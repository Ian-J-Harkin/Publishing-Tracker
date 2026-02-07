import { HttpInterceptorFn } from '@angular/common/http';

export const baseUrlInterceptor: HttpInterceptorFn = (req, next) => {
    const baseUrl = 'http://localhost:5226'; // This should ideally come from environment

    if (!req.url.startsWith('http')) {
        const cloned = req.clone({
            url: `${baseUrl}${req.url.startsWith('/') ? '' : '/'}${req.url}`
        });
        return next(cloned);
    }

    return next(req);
};
