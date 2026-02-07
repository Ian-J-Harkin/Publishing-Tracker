import axiosClient from '../api/axiosClient';
import { DashboardSummary, YoYComparison, SeasonalPerformance } from '../types/dashboard';

const API_URL = '/api/dashboard';

const getDashboardSummary = async (): Promise<DashboardSummary> => {
    const response = await axiosClient.get(`${API_URL}/summary`);
    return response.data;
};

const getYoYComparison = async (): Promise<YoYComparison> => {
    const response = await axiosClient.get(`${API_URL}/yoy`);
    return response.data;
};

const getSeasonalPerformance = async (): Promise<SeasonalPerformance[]> => {
    const response = await axiosClient.get(`${API_URL}/seasonal`);
    return response.data;
};

export const dashboardService = {
    getDashboardSummary,
    getYoYComparison,
    getSeasonalPerformance
};