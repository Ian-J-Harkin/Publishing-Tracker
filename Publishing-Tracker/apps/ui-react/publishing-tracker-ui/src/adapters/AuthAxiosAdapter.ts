import axiosClient from '../api/axiosClient';
import { IAuthPort } from '../ports';

const API_URL = '/api/auth';

export const AuthAxiosAdapter: IAuthPort = {
    register: async (data: Record<string, unknown>) => {
        const response = await axiosClient.post(`${API_URL}/register`, data);
        return response.data;
    },
    login: async (data: Record<string, unknown>) => {
        const response = await axiosClient.post(`${API_URL}/login`, data);
        return response.data;
    }
};
