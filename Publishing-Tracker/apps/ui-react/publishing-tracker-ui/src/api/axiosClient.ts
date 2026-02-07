import axios from 'axios';

const axiosClient = axios.create({
    // @ts-ignore - handled by Vite but may cause issues in Jest
    baseURL: (import.meta as any).env?.VITE_API_BASE_URL || '',
    headers: {
        'Content-Type': 'application/json',
    },
});

axiosClient.interceptors.request.use(
    (config) => {
        const token = localStorage.getItem('token');
        if (token && config.headers) {
            config.headers.Authorization = `Bearer ${token}`;
        }
        return config;
    },
    (error) => Promise.reject(error)
);

export default axiosClient;
