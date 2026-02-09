import { render, screen, waitFor } from '@testing-library/react';
import '@testing-library/jest-dom';
import { BrowserRouter } from 'react-router-dom';
import BookListPage from './BookListPage';
import { bookService } from '../services/bookService';

jest.mock('../services/bookService');
const mockedBookService = bookService as jest.Mocked<typeof bookService>;

describe('BookListPage', () => {
    it('should render the book list page', async () => {
        mockedBookService.getBooks.mockResolvedValue([]);

        render(
            <BrowserRouter>
                <BookListPage />
            </BrowserRouter>
        );

        await waitFor(() => {
            expect(screen.getByRole('heading', { name: /IP Portfolio/i })).toBeInTheDocument();
        });
    });
});