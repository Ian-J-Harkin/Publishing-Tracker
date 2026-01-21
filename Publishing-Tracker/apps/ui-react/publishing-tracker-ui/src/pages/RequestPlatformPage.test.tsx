import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import RequestPlatformPage from './RequestPlatformPage';
import { platformService } from '../services/platformService';

jest.mock('../services/platformService');
const mockedPlatformService = platformService as jest.Mocked<typeof platformService>;

describe('RequestPlatformPage', () => {
    it('should submit a new platform request', async () => {
        mockedPlatformService.requestPlatform.mockResolvedValue();

        render(
            <BrowserRouter>
                <RequestPlatformPage />
            </BrowserRouter>
        );

        fireEvent.change(screen.getByLabelText(/name/i), { target: { value: 'New Platform' } });
        fireEvent.change(screen.getByLabelText(/base url/i), { target: { value: 'new-url' } });
        fireEvent.change(screen.getByLabelText(/commission rate/i), { target: { value: '0.2' } });
        fireEvent.submit(screen.getByRole('button', { name: /submit request/i }));

        await waitFor(() => {
            expect(mockedPlatformService.requestPlatform).toHaveBeenCalledWith({
                name: 'New Platform',
                baseUrl: 'new-url',
                commissionRate: 0.2
            });
        });
    });
});