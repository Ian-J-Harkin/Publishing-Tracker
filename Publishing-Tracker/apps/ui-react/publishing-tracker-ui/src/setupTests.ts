import '@testing-library/jest-dom';

// Use dynamic require and globalThis to avoid TS compilation issues in the test environment
// @ts-ignore
const { TextEncoder: TE, TextDecoder: TD } = require('util');
(globalThis as any).TextEncoder = TE;
(globalThis as any).TextDecoder = TD;

jest.mock('./api/axiosClient', () => ({
    __esModule: true,
    default: {
        interceptors: {
            request: { use: jest.fn(), eject: jest.fn() },
            response: { use: jest.fn(), eject: jest.fn() },
        },
        get: jest.fn(),
        post: jest.fn(),
        put: jest.fn(),
        delete: jest.fn(),
        defaults: { headers: { common: {} } }
    }
}));