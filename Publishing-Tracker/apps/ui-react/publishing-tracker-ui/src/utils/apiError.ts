export function getApiErrorMessage(error: unknown, fallback: string): string {
    if (typeof error === 'string' && error.trim()) {
        return error;
    }

    if (error && typeof error === 'object') {
        const typedError = error as {
            message?: unknown;
            response?: { data?: unknown };
        };

        const responseData = typedError.response?.data;
        if (typeof responseData === 'string' && responseData.trim()) {
            return responseData;
        }

        if (responseData && typeof responseData === 'object') {
            const payload = responseData as { message?: unknown; error?: unknown; title?: unknown };
            if (typeof payload.message === 'string' && payload.message.trim()) return payload.message;
            if (typeof payload.error === 'string' && payload.error.trim()) return payload.error;
            if (typeof payload.title === 'string' && payload.title.trim()) return payload.title;
        }

        if (typeof typedError.message === 'string' && typedError.message.trim()) {
            return typedError.message;
        }
    }

    return fallback;
}
