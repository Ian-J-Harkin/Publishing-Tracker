import axios from 'axios';
import { platformService } from './platformService';
import { Platform, PlatformRequest } from '../types/platform';

jest.mock('axios');
const mockedAxios = axios as jest.Mocked<typeof axios>;

describe('platformService', () => {
    beforeEach(() => {
        localStorage.setItem('token', 'test-token');
    });

    afterEach(() => {
        localStorage.clear();
        jest.clearAllMocks();
    });

    it('should fetch platforms', async () => {
        const platforms: Platform[] = [{ id: 1, name: 'Platform 1', baseUrl: 'url1', commissionRate: 0.1 }];
        mockedAxios.get.mockResolvedValue({ data: platforms });

        const result = await platformService.getPlatforms();

        expect(result).toEqual(platforms);
        expect(mockedAxios.get).toHaveBeenCalledWith(import.meta.env.VITE_API_BASE_URL + '/api/platforms', {
            headers: { Authorization: 'Bearer test-token' }
        });
    });

    it('should request a new platform', async () => {
        const platformRequest: PlatformRequest = { name: 'New Platform', baseUrl: 'new-url', commissionRate: 0.2 };
        mockedAxios.post.mockResolvedValue({});

        await platformService.requestPlatform(platformRequest);

        expect(mockedAxios.post).toHaveBeenCalledWith(import.meta.env.VITE_API_BASE_URL + '/api/platforms/requests', platformRequest, {
            headers: { Authorization: 'Bearer test-token' }
        });
    });
});