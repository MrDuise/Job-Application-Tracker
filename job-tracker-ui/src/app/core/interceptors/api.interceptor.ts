import { HttpInterceptorFn } from '@angular/common/http';

export const apiInterceptor: HttpInterceptorFn = (req, next) => {
  const apiUrl = 'http://localhost:5000';

  if (!req.url.startsWith('http')) {
    const apiReq = req.clone({
      url: `${apiUrl}/${req.url}`,
    });
    return next(apiReq);
  }

  return next(req);
};
