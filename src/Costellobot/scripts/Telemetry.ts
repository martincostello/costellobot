// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

import { getWebInstrumentations, initializeFaro } from '@grafana/faro-web-sdk';
import { TracingInstrumentation } from '@grafana/faro-web-tracing';

export class Telemetry {
    static initialize() {
        const getOption = (name: string): string | null => {
            const meta = document.querySelector(`meta[name='x-telemetry-${name}']`);

            let value: string | null = null;

            if (meta) {
                value = meta.getAttribute('content');
            }

            return value;
        };

        const url = getOption('collector-url');
        const environment = getOption('service-environment');
        const name = getOption('service-name');
        const namespace = getOption('service-namespace');
        const version = getOption('service-version');

        if (!url || !name || !version || !environment) {
            return;
        }

        let tracking = undefined;
        const samplingRate = getOption('sample-rate');

        if (samplingRate) {
            tracking = {
                samplingRate: parseFloat(samplingRate),
            };
        }

        initializeFaro({
            url,
            app: {
                environment,
                name,
                namespace,
                version,
            },
            sessionTracking: tracking,
            instrumentations: [...getWebInstrumentations(), new TracingInstrumentation()],
        });
    }
}
