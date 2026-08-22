import axiosClient from '../api/axiosClient';
import { IAuthPort } from '../ports';
import { AuthResponse, LoginRequest, RegisterRequest } from '../types/auth';

const API_URL = '/api/auth';

export const AuthAxiosAdapter: IAuthPort = {
    register: async (data: RegisterRequest): Promise<AuthResponse> => {
        const response = await axiosClient.post(`${API_URL}/register`, data);
        return response.data;
    },
    login: async (data: LoginRequest): Promise<AuthResponse> => {
        const response = await axiosClient.post(`${API_URL}/login`, data);
        return response.data;
    }
};
