import axiosClient from '../api/axiosClient';
import { ImportJob, ColumnMapping } from '../types/import';

const API_URL = '/api/import';

const uploadFile = async (file: File): Promise<{ fileName: string, headers: string[] }> => {
    const formData = new FormData();
    formData.append('file', file);

    const response = await axiosClient.post(`${API_URL}/upload`, formData, {
        headers: {
            'Content-Type': 'multipart/form-data'
        }
    });
    return response.data;
};

const processFile = async (fileName: string, mapping: ColumnMapping): Promise<ImportJob> => {
    const response = await axiosClient.post(`${API_URL}/process`, { fileName, mapping });
    return response.data;
};

const getHistory = async (): Promise<ImportJob[]> => {
    const response = await axiosClient.get(`${API_URL}/history`);
    return response.data;
};

export const importService = {
    uploadFile,
    processFile,
    getHistory
};