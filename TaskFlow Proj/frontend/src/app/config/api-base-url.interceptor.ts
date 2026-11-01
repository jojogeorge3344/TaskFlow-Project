import { HttpInterceptorFn } from '@angular/common/http';
import { API_BASE_URL } from './api-base-url';

export const apiBaseUrlInterceptor: HttpInterceptorFn = (request, next) =>
  API_BASE_URL !== '' && request.url.startsWith('/api/')
    ? next(request.clone({ url: `${API_BASE_URL}${request.url}`, withCredentials: true }))
    : next(request);
