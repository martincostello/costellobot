// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

import { afterEach, beforeEach, describe, expect, test, vi } from 'vitest';
import { App } from './App';

const signalr = vi.hoisted(() => {
    const handlers = new Map<string, (...args: any[]) => void>();
    const builder = {
        url: '',
        automaticReconnect: false,
    };
    const start = vi.fn(() => Promise.resolve());
    const connection = {
        on(methodName: string, handler: (...args: any[]) => void) {
            handlers.set(methodName, handler);
        },
        start,
    };
    return { builder, connection, handlers, start };
});

vi.mock('@microsoft/signalr', () => {
    class HubConnectionBuilder {
        withUrl(url: string) {
            signalr.builder.url = url;
            return this;
        }

        withAutomaticReconnect() {
            signalr.builder.automaticReconnect = true;
            return this;
        }

        build() {
            return signalr.connection;
        }
    }

    // eslint-disable-next-line @typescript-eslint/naming-convention
    return { HubConnectionBuilder };
});

const applicationLogsMethod = 'application-logs';
const webhookLogsMethod = 'webhook-logs';

function setupLogsPage(): void {
    document.body.innerHTML = `
        <input type="checkbox" id="logs-auto-scroll" />
        <div id="logs"></div>
        <span id="webhooks-count"></span>
        <div id="webhooks-index"></div>
        <div id="webhooks-content"></div>`;
}

function setupDebugPage(withSignature = true): void {
    document.body.innerHTML = `
        <input type="hidden" id="app-id" value="42" />
        <input type="text" id="webhook-event" value="ping" />
        <textarea id="webhook-payload"></textarea>
        ${withSignature ? '<input type="text" id="webhook-signature" value="sha256=abc123" />' : ''}
        <button type="submit" id="post-webhook" disabled>
            <span class="spinner-border d-none"></span>
            Post
        </button>
        <span class="badge webhook-status d-none"></span>`;
}

function raise(element: Element, name: string): void {
    element.dispatchEvent(new Event(name));
}

function setPayload(value: string): void {
    const payload = <HTMLTextAreaElement>document.getElementById('webhook-payload');
    payload.value = value;

    raise(payload, 'input');
}

async function initializeApp(): Promise<App> {
    const app = new App();
    await app.initialize();
    return app;
}

function invoke(methodName: string, ...args: any[]): void {
    const handler = signalr.handlers.get(methodName);
    expect(handler, `No handler was registered for the "${methodName}" method.`).toBeDefined();
    handler!(...args);
}

function webhookHeaders(delivery: string, event: string): Record<string, string> {
    const headers: Record<string, string> = {};

    headers['X-GitHub-Delivery'] = delivery;
    headers['X-GitHub-Event'] = event;

    return headers;
}

function logEntry(overrides: Record<string, any> = {}): Record<string, any> {
    return {
        category: 'MartinCostello.Costellobot.Foo',
        level: 'Information',
        eventId: 42,
        message: 'Something happened.',
        timestamp: '2024-01-02T03:04:05.0000000Z',
        ...overrides,
    };
}

describe('App', () => {
    let consoleError: ReturnType<typeof vi.fn>;

    beforeEach(() => {
        signalr.handlers.clear();
        signalr.builder.url = '';
        signalr.builder.automaticReconnect = false;
        document.body.innerHTML = '';

        // The application writes to the console, which would otherwise pollute the test output.
        consoleError = vi.fn();
        vi.stubGlobal('console', { ...console, error: consoleError, log: vi.fn() });
    });

    afterEach(() => {
        vi.unstubAllGlobals();
        delete (window as any)['ClipboardJS'];
    });

    test('should be defined', () => {
        expect(App).toBeDefined();
    });

    test('connects to the webhook hub', () => {
        new App();

        expect(signalr.builder.url).toBe('/admin/git-hub');
        expect(signalr.builder.automaticReconnect).toBe(true);
    });

    test('does nothing if the page has no logs or webhook form', async () => {
        await initializeApp();

        expect(signalr.handlers.size).toBe(0);
        expect(signalr.start).not.toHaveBeenCalled();
    });

    describe('when the logs page is loaded', () => {
        test('subscribes to the client methods used by the server', async () => {
            setupLogsPage();

            await initializeApp();

            expect([...signalr.handlers.keys()]).toEqual([applicationLogsMethod, webhookLogsMethod]);
            expect(signalr.start).toHaveBeenCalledTimes(1);
        });

        test('logs an error if the connection cannot be started', async () => {
            setupLogsPage();

            const error = new Error('Connection refused.');
            signalr.start.mockImplementationOnce(() => Promise.reject(error));

            await initializeApp();

            expect(consoleError).toHaveBeenCalledWith(error);
        });

        test('adds an application log entry', async () => {
            setupLogsPage();
            await initializeApp();

            invoke(applicationLogsMethod, logEntry());

            const entries = document.querySelectorAll('#logs .log-entry');

            expect(entries.length).toBe(1);
            expect(entries[0].querySelector('.log-timestamp')).not.toBeNull();
            expect(entries[0].querySelector('.log-level-information')).not.toBeNull();
            expect(entries[0].textContent).toContain('MartinCostello.Costellobot.Foo[42]: Something happened.');
            expect(entries[0].querySelector('.log-exception')).toBeNull();
            expect(entries[0].querySelector('.log-link')).toBeNull();
        });

        test('uses the event name if one is specified', async () => {
            setupLogsPage();
            await initializeApp();

            invoke(applicationLogsMethod, logEntry({ eventName: 'SomethingHappened' }));

            const entry = document.querySelector('#logs .log-entry')!;

            expect(entry.textContent).toContain('MartinCostello.Costellobot.Foo[SomethingHappened]: Something happened.');
        });

        test('replaces any existing content the first time a log entry is added', async () => {
            setupLogsPage();
            document.getElementById('logs')!.textContent = 'No logs have been received yet.';

            await initializeApp();

            invoke(applicationLogsMethod, logEntry());
            invoke(applicationLogsMethod, logEntry());

            const logs = document.getElementById('logs')!;

            expect(logs.textContent).not.toContain('No logs have been received yet.');
            expect(logs.querySelectorAll('.log-entry').length).toBe(2);
        });

        test('adds a link for an issue reference in a log entry', async () => {
            setupLogsPage();
            await initializeApp();

            invoke(applicationLogsMethod, logEntry({ message: 'Approved martincostello/costellobot#123 for merge.' }));

            const link = document.querySelector<HTMLAnchorElement>('#logs .log-entry .log-link')!;

            expect(link).not.toBeNull();
            expect(link.href).toBe('https://github.com/martincostello/costellobot/issues/123');
            expect(link.target).toBe('_blank');
        });

        test('adds the exception associated with a log entry', async () => {
            setupLogsPage();
            await initializeApp();

            invoke(applicationLogsMethod, logEntry({ level: 'Error', exception: 'System.InvalidOperationException: Bang.' }));

            const entry = document.querySelector('#logs .log-entry')!;

            expect(entry.querySelector('.log-level-error')).not.toBeNull();
            expect(entry.querySelector('.log-exception')).not.toBeNull();
        });

        test('scrolls to the latest log entry if auto-scroll is enabled', async () => {
            setupLogsPage();

            const logs = document.getElementById('logs')!;
            Object.defineProperty(logs, 'scrollHeight', { value: 1234 });

            const autoScroll = <HTMLInputElement>document.getElementById('logs-auto-scroll');
            autoScroll.checked = true;

            await initializeApp();

            invoke(applicationLogsMethod, logEntry());

            expect(logs.scrollTop).toBe(1234);
        });

        test('does not scroll to the latest log entry if auto-scroll is disabled', async () => {
            setupLogsPage();

            const logs = document.getElementById('logs')!;
            Object.defineProperty(logs, 'scrollHeight', { value: 1234 });

            await initializeApp();

            invoke(applicationLogsMethod, logEntry());

            expect(logs.scrollTop).toBe(0);
        });

        test('adds a webhook delivery', async () => {
            setupLogsPage();
            await initializeApp();

            invoke(webhookLogsMethod, webhookHeaders('my-delivery', 'ping'), { zen: 'Keep it logically awesome.' });

            const index = document.querySelector('#webhooks-index .webhook-item')!;

            expect(index).not.toBeNull();
            expect(index.getAttribute('id')).toBe('webhook-index-my-delivery');
            expect(index.getAttribute('href')).toBe('#webhook-content-my-delivery');
            expect(index.getAttribute('aria-controls')).toBe('webhook-content-my-delivery');
            expect(index.getAttribute('x-github-delivery')).toBe('my-delivery');
            expect(index.getAttribute('x-github-event')).toBe('ping');
            expect(index.querySelector('.x-github-delivery')?.textContent).toBe('my-delivery');
            expect(index.querySelector('.x-github-event')?.textContent).toBe('ping');

            const content = document.getElementById('webhook-content-my-delivery')!;

            expect(content).not.toBeNull();
            expect(content.getAttribute('aria-labelledby')).toBe('webhook-index-my-delivery');

            const code = content.querySelector('.webhook-content')!;

            expect(code).not.toBeNull();
            expect(code.getAttribute('x-github-delivery')).toBe('my-delivery');
            expect(code.getAttribute('x-github-event')).toBe('ping');

            expect(document.getElementById('webhooks-count')!.innerText).toBe('(1)');
        });

        test('adds the most recent webhook delivery first', async () => {
            setupLogsPage();
            await initializeApp();

            invoke(webhookLogsMethod, webhookHeaders('first', 'ping'), {});
            invoke(webhookLogsMethod, webhookHeaders('second', 'push'), {});

            const deliveries = [...document.querySelectorAll('#webhooks-index .webhook-item')].map((p) =>
                p.getAttribute('x-github-delivery')
            );

            expect(deliveries).toEqual(['second', 'first']);
            expect(document.getElementById('webhooks-content')!.children.length).toBe(2);
            expect(document.getElementById('webhooks-count')!.innerText).toBe('(2)');
        });
    });

    describe('when the debug page is loaded', () => {
        test('enables the submit button if the payload is valid JSON', async () => {
            setupDebugPage();
            await initializeApp();

            const submit = document.getElementById('post-webhook')!;

            setPayload('{ "zen": "Keep it logically awesome." }');

            expect(submit.hasAttribute('disabled')).toBe(false);
        });

        test('disables the submit button if the payload is not valid JSON', async () => {
            setupDebugPage();
            await initializeApp();

            const payload = <HTMLTextAreaElement>document.getElementById('webhook-payload');
            const submit = document.getElementById('post-webhook')!;

            setPayload('{ "zen": "Keep it logically awesome." }');

            payload.value = '{ "zen": ';
            raise(payload, 'change');

            expect(submit.hasAttribute('disabled')).toBe(true);
        });

        test('does not subscribe to the webhook hub', async () => {
            setupDebugPage();

            await initializeApp();

            expect(signalr.handlers.size).toBe(0);
            expect(signalr.start).not.toHaveBeenCalled();
        });

        test('posts the webhook and displays the response status code', async () => {
            setupDebugPage();
            await initializeApp();

            const fetch = vi.fn(() => Promise.resolve({ ok: true, status: 202 }));
            vi.stubGlobal('fetch', fetch);

            setPayload('{ "zen": "Keep it logically awesome." }');

            const submit = document.getElementById('post-webhook')!;
            submit.click();

            await vi.waitFor(() => expect(fetch).toHaveBeenCalledTimes(1));

            const [url, init] = (fetch.mock.calls as any)[0];
            const headers = <Headers>init.headers;

            expect(url).toBe('/github-webhook');
            expect(init.method).toBe('POST');
            expect(init.body).toBe(JSON.stringify({ zen: 'Keep it logically awesome.' }));

            expect(headers.get('Accept')).toBe('application/json');
            expect(headers.get('Content-Type')).toBe('application/json');
            expect(headers.get('X-GitHub-Delivery')).toBe('debug-1');
            expect(headers.get('X-GitHub-Event')).toBe('ping');
            expect(headers.get('X-GitHub-Hook-ID')).toBe('debug');
            expect(headers.get('X-GitHub-Hook-Installation-Target-ID')).toBe('42');
            expect(headers.get('X-GitHub-Hook-Installation-Target-Type')).toBe('integration');
            expect(headers.get('X-Hub-Signature-256')).toBe('sha256=abc123');

            const badge = document.querySelector('.webhook-status')!;

            await vi.waitFor(() => expect(badge.textContent).toBe('202'));

            expect(badge.classList.contains('bg-success')).toBe(true);
            expect(badge.classList.contains('bg-danger')).toBe(false);
            expect(badge.classList.contains('d-none')).toBe(false);

            expect(submit.hasAttribute('disabled')).toBe(false);
            expect(submit.querySelector('.spinner-border')!.classList.contains('d-none')).toBe(true);
        });

        test('displays a failure if the webhook is not accepted', async () => {
            setupDebugPage();
            await initializeApp();

            const fetch = vi.fn(() => Promise.resolve({ ok: false, status: 400 }));
            vi.stubGlobal('fetch', fetch);

            setPayload('{}');

            document.getElementById('post-webhook')!.click();

            const badge = document.querySelector('.webhook-status')!;

            await vi.waitFor(() => expect(badge.textContent).toBe('400'));

            expect(badge.classList.contains('bg-danger')).toBe(true);
            expect(badge.classList.contains('bg-success')).toBe(false);
        });

        test('appends the problem details title to the badge if the response is not accepted', async () => {
            setupDebugPage();
            await initializeApp();

            const problemDetails = {
                type: 'https://tools.ietf.org/html/rfc9110#section-15.5.5',
                title: 'Bad Request',
                status: 400,
                detail: 'The webhook payload was invalid.',
                instance: '/github-webhook',
            };

            const fetch = vi.fn(() =>
                Promise.resolve({
                    ok: false,
                    status: 400,
                    json: () => Promise.resolve(problemDetails),
                })
            );
            vi.stubGlobal('fetch', fetch);

            setPayload('{}');

            document.getElementById('post-webhook')!.click();

            const badge = document.querySelector('.webhook-status')!;

            await vi.waitFor(() => expect(badge.textContent).toBe('400 - Bad Request'));

            expect(badge.classList.contains('bg-danger')).toBe(true);
            expect(badge.classList.contains('bg-success')).toBe(false);
        });

        test('does not append a problem details title if the response body is not JSON', async () => {
            setupDebugPage();
            await initializeApp();

            const fetch = vi.fn(() =>
                Promise.resolve({
                    ok: false,
                    status: 500,
                    json: () => Promise.reject(new Error('Not JSON.')),
                })
            );
            vi.stubGlobal('fetch', fetch);

            setPayload('{}');

            document.getElementById('post-webhook')!.click();

            const badge = document.querySelector('.webhook-status')!;

            await vi.waitFor(() => expect(badge.textContent).toBe('500'));

            expect(badge.classList.contains('bg-danger')).toBe(true);
        });

        test('omits the signature if there is no signature input', async () => {
            setupDebugPage(false);
            await initializeApp();

            const fetch = vi.fn(() => Promise.resolve({ ok: true, status: 202 }));
            vi.stubGlobal('fetch', fetch);

            setPayload('{}');

            document.getElementById('post-webhook')!.click();

            await vi.waitFor(() => expect(fetch).toHaveBeenCalledTimes(1));

            const headers = <Headers>(fetch.mock.calls as any)[0][1].headers;

            expect(headers.has('X-Hub-Signature-256')).toBe(false);
        });
    });

    describe('when clipboard.js is loaded', () => {
        test('sets up the copy button', async () => {
            document.body.innerHTML = '<button class="copy-button" data-clipboard-target="#value">Copy</button>';

            const clipboard = vi.fn();
            (window as any)['ClipboardJS'] = clipboard;

            await initializeApp();

            expect(clipboard).toHaveBeenCalledWith('.copy-button');

            const event = new Event('click', { cancelable: true });
            document.querySelector('.copy-button')!.dispatchEvent(event);

            expect(event.defaultPrevented).toBe(true);
        });

        test('does nothing if there is no copy button', async () => {
            const clipboard = vi.fn();
            (window as any)['ClipboardJS'] = clipboard;

            await initializeApp();

            expect(clipboard).toHaveBeenCalledWith('.copy-button');
        });
    });
});
