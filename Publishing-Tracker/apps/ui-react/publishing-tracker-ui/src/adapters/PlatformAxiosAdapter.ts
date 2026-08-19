import axiosClient from '../api/axiosClient';
import { Platform, PlatformRequest } from '../types/platform';
import { IPlatformPort } from '../ports';

const API_URL = '/api/platforms';

export const PlatformAxiosAdapter: IPlatformPort = {
    getPlatforms: async (search?: string): Promise<Platform[]> => {
        const response = await axiosClient.get(API_URL, { params: { search } });
        return response.data;
    },
    getAdminPlatforms: async (search?: string): Promise<Platform[]> => {
        const response = await axiosClient.get(API_URL, { params: { search, includeInactive: true } });
        return response.data;
    },
    requestPlatform: async (platformRequest: PlatformRequest): Promise<void> => {
        await axiosClient.post(API_URL, platformRequest);
    },
    approvePlatform: async (id: number): Promise<void> => {
        await axiosClient.put(`${API_URL}/${id}/approve`);
    }
};