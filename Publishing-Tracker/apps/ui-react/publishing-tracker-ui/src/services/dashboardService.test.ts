import axiosClient from '../api/axiosClient';
import { dashboardService } from './dashboardService';
import { DashboardSummary } from '../types/dashboard';

jest.mock('../api/axiosClient');
const mockedAxiosClient = axiosClient as jest.Mocked<typeof axiosClient>;

describe('dashboardService', () => {
    afterEach(() => {
        jest.clearAllMocks();
    });

    it('should fetch dashboard summary', async () => {
        const summary: DashboardSummary = {
            totalRevenue: 1000,
            totalBooksPublished: 10,
            totalSalesTransactions: 100,
            topPerformingBook: 'Book 1',
            topPerformingPlatform: 'Platform 1'
        };
        mockedAxiosClient.get.mockResolvedValue({ data: summary });

        const result = await dashboardService.getDashboardSummary();

        expect(result).toEqual(summary);
        expect(mockedAxiosClient.get).toHaveBeenCalledWith('/api/dashboard/summary');
    });

    it('should fetch year-over-year comparison', async () => {
        const yoy = { currentYearRevenue: 1000, previousYearRevenue: 800, growth: 0.25 };
        mockedAxiosClient.get.mockResolvedValue({ data: yoy });

        const result = await dashboardService.getYoYComparison();

        expect(result).toEqual(yoy);
        expect(mockedAxiosClient.get).toHaveBeenCalledWith('/api/dashboard/yoy');
    });

    it('should fetch seasonal performance', async () => {
        const seasonal = [{ month: 1, totalRevenue: 100 }];
        mockedAxiosClient.get.mockResolvedValue({ data: seasonal });

        const result = await dashboardService.getSeasonalPerformance();

        expect(result).toEqual(seasonal);
        expect(mockedAxiosClient.get).toHaveBeenCalledWith('/api/dashboard/seasonal');
    });
});