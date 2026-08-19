import axiosClient from '../api/axiosClient';
import { ImportJob, ColumnMapping, PreviewData } from '../types/import';
import { IImportPort } from '../ports';

const API_URL = '/api/import';

export const ImportAxiosAdapter: IImportPort = {
    uploadFile: async (file: File): Promise<PreviewData> => {
        const formData = new FormData();
        formData.append('file', file);

        const response = await axiosClient.post<PreviewData>(`${API_URL}/upload`, formData, {
            headers: {
                'Content-Type': 'multipart/form-data'
            }
        });
        return response.data;
    },
    processFile: async (fileName: string, mapping: ColumnMapping): Promise<ImportJob> => {
        const response = await axiosClient.post(`${API_URL}/process`, { fileName, mapping });
        return response.data;
    },
    getHistory: async (): Promise<ImportJob[]> => {
        const response = await axiosClient.get(`${API_URL}/history`);
        return response.data;
    }
};