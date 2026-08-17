import axiosClient from '../api/axiosClient';

export interface User {
    id: number;
    email: string;
    firstName: string;
    lastName: string;
    role: string;
    createdAt: string;
}

const API_URL = '/api/users';

const getUsers = async (): Promise<User[]> => {
    const response = await axiosClient.get(API_URL);
    return response.data;
};

const promoteUser = async (id: number): Promise<void> => {
    await axiosClient.put(`${API_URL}/${id}/promote`);
};

const demoteUser = async (id: number): Promise<void> => {
    await axiosClient.put(`${API_URL}/${id}/demote`);
};

export const userService = {
    getUsers,
    promoteUser,
    demoteUser
};
