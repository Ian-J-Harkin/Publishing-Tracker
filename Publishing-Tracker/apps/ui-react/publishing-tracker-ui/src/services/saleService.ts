import axiosClient from '../api/axiosClient';
import { Sale, CreateSale, SalesSummary, SalesFilter } from '../types/sale';

const API_URL = '/api/sales';

const getSales = async (filters: SalesFilter = {}): Promise<Sale[]> => {
    const response = await axiosClient.get(API_URL, { params: filters });
    return response.data;
};

const getSummary = async (filters: { startDate?: string; endDate?: string } = {}): Promise<SalesSummary> => {
    const response = await axiosClient.get(`${API_URL}/summary`, { params: filters });
    return response.data;
};

const createSale = async (sale: CreateSale): Promise<Sale> => {
    const response = await axiosClient.post(API_URL, sale);
    return response.data;
};

export const saleService = {
    getSales,
    getSummary,
    createSale
};