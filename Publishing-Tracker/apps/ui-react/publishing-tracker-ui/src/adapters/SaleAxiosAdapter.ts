import axiosClient from '../api/axiosClient';
import { Sale, CreateSale, SalesSummary, SalesFilter } from '../types/sale';
import { ISalePort } from '../ports';

const API_URL = '/api/sales';

export const SaleAxiosAdapter: ISalePort = {
    getSales: async (filters: SalesFilter = {}): Promise<Sale[]> => {
        const response = await axiosClient.get(API_URL, { params: filters });
        return response.data;
    },
    getSummary: async (filters: { startDate?: string; endDate?: string } = {}): Promise<SalesSummary> => {
        const response = await axiosClient.get(`${API_URL}/summary`, { params: filters });
        return response.data;
    },
    createSale: async (sale: CreateSale): Promise<Sale> => {
        const response = await axiosClient.post(API_URL, sale);
        return response.data;
    }
};