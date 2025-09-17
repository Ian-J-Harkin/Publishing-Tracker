import axios from 'axios';
import { dashboardService } from './dashboardService';
import { DashboardSummary } from '../types/dashboard';

jest.mock('axios');
const mockedAxios = axios as jest.Mocked<typeof axios>;

describe('dashboardService', () => {
    beforeEach(() => {
        localStorage.setItem('token', 'test-token');
    });

    afterEach(() => {
        localStorage.clear();
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
        mockedAxios.get.mockResolvedValue({ data: summary });

        const result = await dashboardService.getDashboardSummary();

        expect(result).toEqual(summary);
        expect(mockedAxios.get).toHaveBeenCalledWith(import.meta.env.VITE_API_BASE_URL + '/api/dashboard/summary', {
            headers: { Authorization: 'Bearer test-token' }
        });
    });

    it('should fetch year-over-year comparison', async () => {
        const yoy = { currentYearRevenue: 1000, previousYearRevenue: 800, growth: 0.25 };
        mockedAxios.get.mockResolvedValue({ data: yoy });

        const result = await dashboardService.getYoYComparison();

        expect(result).toEqual(yoy);
        expect(mockedAxios.get).toHaveBeenCalledWith(import.meta.env.VITE_API_BASE_URL + '/api/dashboard/yoy', {
            headers: { Authorization: 'Bearer test-token' }
        });
    });

    it('should fetch seasonal performance', async () => {
        const seasonal = [{ month: 1, totalRevenue: 100 }];
        mockedAxios.get.mockResolvedValue({ data: seasonal });

        const result = await dashboardService.getSeasonalPerformance();

        expect(result).toEqual(seasonal);
        expect(mockedAxios.get).toHaveBeenCalledWith(import.meta.env.VITE_API_BASE_URL + '/api/dashboard/seasonal', {
            headers: { Authorization: 'Bearer test-token' }
        });
    });
});