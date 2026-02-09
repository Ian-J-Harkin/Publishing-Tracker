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

        fireEvent.change(screen.getByLabelText(/Platform Identity/i), { target: { value: 'New Platform' } });
        fireEvent.change(screen.getByLabelText(/Base Distribution URL/i), { target: { value: 'https://example.com' } });
        fireEvent.change(screen.getByLabelText(/Average Commission Rate/i), { target: { value: '0.2' } });
        fireEvent.submit(screen.getByRole('button', { name: /Commit Request/i }));

        await waitFor(() => {
            expect(mockedPlatformService.requestPlatform).toHaveBeenCalledWith({
                name: 'New Platform',
                baseUrl: 'https://example.com',
                commissionRate: 0.2
            });
        });
    });
});