import axiosClient from '../api/axiosClient';
import { Platform, PlatformRequest } from '../types/platform';

const API_URL = '/api/platforms';

const getPlatforms = async (search?: string): Promise<Platform[]> => {
    const response = await axiosClient.get(API_URL, { params: { search } });
    return response.data;
};

const requestPlatform = async (platformRequest: PlatformRequest): Promise<void> => {
    // The backend MapPost is mapped to the root of the platforms group: /api/platforms
    await axiosClient.post(API_URL, platformRequest);
};

export const platformService = {
    getPlatforms,
    requestPlatform
};