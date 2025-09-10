import axios from 'axios';
import { Platform, PlatformRequest } from '../types/platform';

const API_URL = 'http://localhost:5000/api/platforms';

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
    await axios.post(`${API_URL}/requests`, platformRequest, {
        headers: {
            Authorization: `Bearer ${token}`
        }
    });
};

export const platformService = {
    getPlatforms,
    requestPlatform
};