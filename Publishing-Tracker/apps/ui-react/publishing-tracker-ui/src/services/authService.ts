import axiosClient from '../api/axiosClient';

const API_URL = '/api/auth';

const register = (data: Record<string, unknown>) => {
    return axiosClient.post(`${API_URL}/register`, data);
};

const login = (data: Record<string, unknown>) => {
    return axiosClient.post(`${API_URL}/login`, data);
};

const authService = {
    register,
    login,
};

export default authService;
