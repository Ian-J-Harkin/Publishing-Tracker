import axios from 'axios';

const API_URL = import.meta.env.VITE_API_BASE_URL + '/api/auth';

const register = (data: any) => {
    return axios.post(`${API_URL}/register`, data);
};

const login = (data: any) => {
    return axios.post(`${API_URL}/login`, data);
};

const authService = {
    register,
    login,
};

export default authService;
