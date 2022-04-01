// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

import { App } from './App';

document.addEventListener('DOMContentLoaded', async () => {
    const app = new App();
    await app.initialize();
});
