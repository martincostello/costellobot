// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

import * as signalR from '@microsoft/signalr';
import * as moment from 'moment';
import 'moment/locale/en-gb';

export class App {

    private readonly connection: signalR.HubConnection;

    private logsContainer: HTMLInputElement;
    private webhooksIndexContainer: HTMLElement;
    private webhooksContentContainer: HTMLElement;
    private webhookIndex: number = 1;

    constructor() {
        this.connection = new signalR.HubConnectionBuilder()
            .withUrl('/admin/git-hub')
            .withAutomaticReconnect()
            .build();
    }

    async initialize(): Promise<void> {

        this.logsContainer = <HTMLInputElement>document.getElementById('logs');
        this.webhooksIndexContainer = document.getElementById('webhooks-index');
        this.webhooksContentContainer = document.getElementById('webhooks-content');

        if (!this.logsContainer) {
            return;
        }

        moment.locale('en-gb');

        this.connection.on('application-logs', (logEntry: LogEntry) => {
            this.onApplicationLog(logEntry);
        });

        this.connection.on('webhook-logs', (webhookHeaders: any, webhookEvent: any) => {
            this.onWebhook(webhookHeaders, webhookEvent);
        });

        await this.connection
            .start()
            .catch((reason: any) => console.error(reason));
    }

    private onApplicationLog(logEntry: LogEntry) {
        this.addLog(logEntry);
        console.log('Received log entry.', logEntry);
    }

    private onWebhook(webhookHeaders: any, webhookEvent: any) {
        this.addWebhook(webhookHeaders, webhookEvent);
        console.log('Received webhook.', webhookHeaders, webhookEvent);
    }

    private addWebhook(webhookHeaders: any, webhookEvent: any) {

        const id = this.webhookIndex++;
        const indexId = `webhook-index-${id}`;
        const contentId = `webhook-content-${id}`;

        const indexItem = document.createElement('a');

        const timestamp = moment();

        const delivery = webhookHeaders['X-GitHub-Delivery'];
        const event = webhookHeaders['X-GitHub-Event'];

        indexItem.textContent = `${event} (${delivery})`;

        indexItem.classList.add('list-group-item');
        indexItem.classList.add('list-group-item-action');

        indexItem.setAttribute('id', indexId);
        indexItem.setAttribute('data-toggle', 'list');
        indexItem.setAttribute('href', `#${contentId}`);
        indexItem.setAttribute('role', 'tab');
        indexItem.setAttribute('aria-controls', contentId);
        indexItem.setAttribute('aria-selected', 'false');
        indexItem.setAttribute('title', timestamp.toISOString());

        const contentItem = document.createElement('div');

        contentItem.classList.add('tab-pane');
        contentItem.classList.add('fade');

        contentItem.setAttribute('id', contentId);
        contentItem.setAttribute('role', 'tabpanel');
        contentItem.setAttribute('aria-labelledby', indexId);

        const pre = document.createElement('pre');

        const code = document.createElement('code');
        code.classList.add('webhook-content');
        code.classList.add('language-json');
        code.setAttribute('data-lang', 'json');

        let text = JSON.stringify(webhookHeaders, null, 2);
        text += '\n';
        text += JSON.stringify(webhookEvent, null, 2);

        code.innerText = text;

        pre.appendChild(code);
        contentItem.appendChild(pre);

        this.webhooksIndexContainer.appendChild(indexItem);
        this.webhooksContentContainer.appendChild(contentItem);
    }

    private addLog(logEntry: LogEntry) {

        const event = logEntry.eventName ?? logEntry.eventId;
        const timestamp = moment(logEntry.timestamp);

        if (this.logsContainer.textContent) {
            this.logsContainer.textContent += '\n';
        }

        this.logsContainer.textContent += `${timestamp.toISOString()} [${logEntry.level}] ${logEntry.category}[${event}]: ${logEntry.message}`;
        this.logsContainer.scrollTop = this.logsContainer.scrollHeight;
    }
}

interface LogEntry {
    category: string;
    level: string;
    eventId: number;
    eventName?: string;
    message: string;
    timestamp: string;
}
