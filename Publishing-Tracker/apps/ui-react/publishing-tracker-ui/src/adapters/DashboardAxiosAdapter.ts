import axiosClient from '../api/axiosClient';
import { DashboardSummary, YoYComparison, SeasonalPerformance, DashboardData } from '../types/dashboard';
import { IDashboardPort } from '../ports';

const API_URL = '/api/dashboard';

export const DashboardAxiosAdapter: IDashboardPort = {
    getDashboardData: async (): Promise<DashboardData> => {
        const response = await axiosClient.get(`${API_URL}/all`);
        return response.data;
    },
    getDashboardSummary: async (): Promise<DashboardSummary> => {
        const response = await axiosClient.get(`${API_URL}/summary`);
        return response.data;
    },
    getYoYComparison: async (): Promise<YoYComparison> => {
        const response = await axiosClient.get(`${API_URL}/yoy`);
        return response.data;
    },
    getSeasonalPerformance: async (): Promise<SeasonalPerformance[]> => {
        const response = await axiosClient.get(`${API_URL}/seasonal`);
        return response.data;
    }
};