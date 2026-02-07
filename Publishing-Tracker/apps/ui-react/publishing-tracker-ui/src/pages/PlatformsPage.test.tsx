import { render, screen, waitFor } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import PlatformsPage from './PlatformsPage';
import { platformService } from '../services/platformService';
import { Platform } from '../types/platform';

jest.mock('../services/platformService');
const mockedPlatformService = platformService as jest.Mocked<typeof platformService>;

describe('PlatformsPage', () => {
    it('should render platforms', async () => {
        const platforms: Platform[] = [{ id: 1, name: 'Platform 1', baseUrl: 'url1', commissionRate: 0.1 }];
        mockedPlatformService.getPlatforms.mockResolvedValue(platforms);

        render(
            <BrowserRouter>
                <PlatformsPage />
            </BrowserRouter>
        );

        await waitFor(() => {
            expect(screen.getByText('Platform 1')).toBeInTheDocument();
            expect(screen.getByText('url1')).toBeInTheDocument();
        });
    });
});