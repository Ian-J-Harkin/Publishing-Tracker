import axiosClient from '../api/axiosClient';
import { Platform, PlatformRequest } from '../types/platform';

const API_URL = '/api/platforms';

const getPlatforms = async (): Promise<Platform[]> => {
    const response = await axiosClient.get(API_URL);
    return response.data;
};

const requestPlatform = async (platformRequest: PlatformRequest): Promise<void> => {
    await axiosClient.post(`${API_URL}/requests`, platformRequest);
};

export const platformService = {
    getPlatforms,
    requestPlatform
};