import '@testing-library/jest-dom';

// eslint-disable-next-line @typescript-eslint/no-require-imports
const { TextEncoder, TextDecoder } = require('util');

globalThis.TextEncoder = TextEncoder;
// eslint-disable-next-line @typescript-eslint/no-explicit-any
globalThis.TextDecoder = TextDecoder as any;

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