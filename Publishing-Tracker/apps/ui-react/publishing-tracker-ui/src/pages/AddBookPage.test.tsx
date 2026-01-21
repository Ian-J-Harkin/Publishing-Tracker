import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import '@testing-library/jest-dom';
import { BrowserRouter } from 'react-router-dom';
import AddBookPage from './AddBookPage';
import { bookService } from '../services/bookService';

jest.mock('../services/bookService');
const mockedBookService = bookService as jest.Mocked<typeof bookService>;

describe('AddBookPage', () => {
    it('should submit a new book', async () => {
        mockedBookService.createBook.mockResolvedValue({} as any);

        render(
            <BrowserRouter>
                <AddBookPage />
            </BrowserRouter>
        );

        await userEvent.type(screen.getByLabelText(/title/i), 'New Book');
        await userEvent.type(screen.getByLabelText(/author/i), 'New Author');
        await userEvent.type(screen.getByLabelText(/isbn/i), '1234567890');
        await userEvent.type(screen.getByLabelText(/genre/i), 'Fiction');
        await userEvent.type(screen.getByLabelText(/description/i), 'A great book');
        fireEvent.change(screen.getByLabelText(/publication date/i), { target: { value: '2023-01-01' } });
        await userEvent.type(screen.getByLabelText(/base price/i), '19.99');

        fireEvent.submit(screen.getByRole('button', { name: /add book/i }));

        await waitFor(() => {
            expect(mockedBookService.createBook).toHaveBeenCalledWith({
                title: 'New Book',
                author: 'New Author',
                isbn: '1234567890',
                publicationDate: new Date('2023-01-01T00:00:00.000Z'),
                basePrice: 19.99,
                genre: 'Fiction',
                description: 'A great book'
            });
        });
    });
});