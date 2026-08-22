import axiosClient from '../api/axiosClient';
import { IUserPort } from '../ports';
import { User } from '../types/user';

const API_URL = '/api/users';

export const UserAxiosAdapter: IUserPort = {
    getUsers: async (): Promise<User[]> => {
        const response = await axiosClient.get(API_URL);
        return response.data;
    },
    promoteUser: async (id: number): Promise<void> => {
        await axiosClient.put(`${API_URL}/${id}/promote`);
    },
    demoteUser: async (id: number): Promise<void> => {
        await axiosClient.put(`${API_URL}/${id}/demote`);
    },
    getProfile: async (): Promise<User> => {
        const response = await axiosClient.get(`${API_URL}/me`);
        return response.data;
    },
    updateProfile: async (firstName: string, lastName: string): Promise<void> => {
        await axiosClient.put(`${API_URL}/me/profile`, { firstName, lastName });
    },
    changePassword: async (oldPassword: string, newPassword: string): Promise<void> => {
        await axiosClient.put(`${API_URL}/me/password`, { oldPassword, newPassword });
    }
};
