import axios from 'axios';
import { DashboardSummary } from '../types/dashboard';

const API_URL = 'http://localhost:5000/api/dashboard';

const getAuthToken = () => {
    return localStorage.getItem('token');
};

const getDashboardSummary = async (): Promise<DashboardSummary> => {
    const token = getAuthToken();
    const response = await axios.get(`${API_URL}/summary`, {
        headers: {
            Authorization: `Bearer ${token}`
        }
    });
    return response.data;
};

const getYoYComparison = async (): Promise<any> => {
    const token = getAuthToken();
    const response = await axios.get(`${API_URL}/yoy`, {
        headers: {
            Authorization: `Bearer ${token}`
        }
    });
    return response.data;
};

const getSeasonalPerformance = async (): Promise<any> => {
    const token = getAuthToken();
    const response = await axios.get(`${API_URL}/seasonal`, {
        headers: {
            Authorization: `Bearer ${token}`
        }
    });
    return response.data;
};

export const dashboardService = {
    getDashboardSummary,
    getYoYComparison,
    getSeasonalPerformance
};