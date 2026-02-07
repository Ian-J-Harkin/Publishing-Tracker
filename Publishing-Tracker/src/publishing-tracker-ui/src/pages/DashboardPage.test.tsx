import { render, screen, waitFor } from '@testing-library/react';
import '@testing-library/jest-dom';
import { BrowserRouter } from 'react-router-dom';
import DashboardPage from './DashboardPage';
import { useDashboardData } from '../hooks/useDashboardData';
import { AuthProvider } from '../context/AuthContext';

jest.mock('../hooks/useDashboardData');
const mockedUseDashboardData = useDashboardData as jest.Mock<ReturnType<typeof useDashboardData>>;

describe('DashboardPage', () => {
    it('should render dashboard data', async () => {
        mockedUseDashboardData.mockReturnValue({
            summary: {
                totalRevenue: 1000,
                totalBooksPublished: 10,
                totalSalesTransactions: 100,
                topPerformingBook: 'Book 1',
                topPerformingPlatform: 'Platform 1'
            },
            yoy: { currentYearRevenue: 1000, lastYearRevenue: 800, growth: 0.25 },
            seasonal: [{ month: 1, totalRevenue: 100 }],
            loading: false,
            error: false
        });

        render(
            <BrowserRouter>
                <AuthProvider>
                    <DashboardPage />
                </AuthProvider>
            </BrowserRouter>
        );

        await waitFor(() => {
            expect(screen.getByText('Total Revenue')).toBeInTheDocument();
            expect(screen.getByText('$1000.00')).toBeInTheDocument();
        });
    });
});