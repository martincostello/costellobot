// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

import { defineConfig } from 'vitest/config';

export default defineConfig({
    test: {
        clearMocks: true,
        coverage: {
            enabled: true,
            provider: 'v8',
            reporter: ['text', 'html', 'lcov'],
            include: ['scripts/**/*.ts'],
            exclude: ['scripts/**/*.test.ts'],
        },
        reporters: ['default', 'github-actions'],
    },
});
