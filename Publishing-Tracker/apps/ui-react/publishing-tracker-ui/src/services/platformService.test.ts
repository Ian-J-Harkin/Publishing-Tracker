import axiosClient from '../api/axiosClient';
import { platformService } from './platformService';
import { Platform } from '../types/platform';

jest.mock('../api/axiosClient');
const mockedAxiosClient = axiosClient as jest.Mocked<typeof axiosClient>;

describe('platformService', () => {
    afterEach(() => {
        jest.clearAllMocks();
    });

    it('should fetch platforms', async () => {
        const platforms: Platform[] = [{ id: 1, name: 'Platform 1', baseUrl: 'url1', commissionRate: 0.1 }];
        mockedAxiosClient.get.mockResolvedValue({ data: platforms });

        const result = await platformService.getPlatforms();

        expect(result).toEqual(platforms);
        expect(mockedAxiosClient.get).toHaveBeenCalledWith('/api/platforms', { params: { search: undefined } });
    });

    it('should request a new platform', async () => {
        const platformRequest = { name: 'New Platform', baseUrl: 'new-url', commissionRate: 0.2 };
        mockedAxiosClient.post.mockResolvedValue({});

        await platformService.requestPlatform(platformRequest);

        expect(mockedAxiosClient.post).toHaveBeenCalledWith('/api/platforms', platformRequest);
    });
});