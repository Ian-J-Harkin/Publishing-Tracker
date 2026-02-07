import axiosClient from '../api/axiosClient';

const API_URL = '/api/auth';

const register = (data: any) => {
    return axiosClient.post(`${API_URL}/register`, data);
};

const login = (data: any) => {
    return axiosClient.post(`${API_URL}/login`, data);
};

const authService = {
    register,
    login,
};

export default authService;
