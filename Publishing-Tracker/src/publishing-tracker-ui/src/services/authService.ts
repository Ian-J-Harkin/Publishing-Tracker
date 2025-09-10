import axios from 'axios';

const API_URL = 'http://localhost:5226/api/auth';

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