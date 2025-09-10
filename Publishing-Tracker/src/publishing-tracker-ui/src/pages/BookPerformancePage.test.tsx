import { render, screen, waitFor } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import BookPerformancePage from './BookPerformancePage';
import { bookService } from '../services/bookService';

jest.mock('../services/bookService');
const mockedBookService = bookService as jest.Mocked<typeof bookService>;

jest.mock('react-router-dom', () => ({
    ...jest.requireActual('react-router-dom'),
    useParams: () => ({
        id: '1',
    }),
}));

describe('BookPerformancePage', () => {
    it('should render book performance data', async () => {
        const performanceData = [{ platformName: 'Platform 1', totalRevenue: 100 }];
        mockedBookService.getBookPerformance.mockResolvedValue(performanceData);

        render(
            <BrowserRouter>
                <BookPerformancePage />
            </BrowserRouter>
        );

        await waitFor(() => {
            expect(screen.getByText('Platform 1: $100.00')).toBeInTheDocument();
        });
    });
});