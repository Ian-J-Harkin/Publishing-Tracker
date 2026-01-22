import axios from 'axios';
import { Platform, PlatformRequest } from '../types/platform';

const API_URL = import.meta.env.VITE_API_BASE_URL + '/api/platforms';

const getAuthToken = () => {
    return localStorage.getItem('token');
};

const getPlatforms = async (): Promise<Platform[]> => {
    const token = getAuthToken();
    const response = await axios.get(API_URL, {
        headers: {
            Authorization: `Bearer ${token}`
        }
    });
    return response.data;
};

const requestPlatform = async (platformRequest: PlatformRequest): Promise<void> => {
    const token = getAuthToken();
    await axios.post(`${API_URL}/`, platformRequest, {
        headers: {
            Authorization: `Bearer ${token}`
        }
    });
};

export const platformService = {
    getPlatforms,
    requestPlatform
};