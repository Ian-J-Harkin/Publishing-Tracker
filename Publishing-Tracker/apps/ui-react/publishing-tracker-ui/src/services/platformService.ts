import axiosClient from '../api/axiosClient';
import { Platform, PlatformRequest } from '../types/platform';

const API_URL = '/api/platforms';

const getPlatforms = async (search?: string): Promise<Platform[]> => {
    const response = await axiosClient.get(API_URL, { params: { search } });
    return response.data;
};

const getAdminPlatforms = async (search?: string): Promise<Platform[]> => {
    const response = await axiosClient.get(API_URL, { params: { search, includeInactive: true } });
    return response.data;
};

const requestPlatform = async (platformRequest: PlatformRequest): Promise<void> => {
    await axiosClient.post(API_URL, platformRequest);
};

const approvePlatform = async (id: number): Promise<void> => {
    await axiosClient.put(`${API_URL}/${id}/approve`);
};

export const platformService = {
    getPlatforms,
    getAdminPlatforms,
    requestPlatform,
    approvePlatform
};