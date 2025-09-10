import axios from 'axios';
import { ImportJob, ColumnMapping } from '../types/import';

const API_URL = 'http://localhost:5000/api/import';

const getAuthToken = () => {
    return localStorage.getItem('token');
};

const uploadFile = async (file: File): Promise<{ fileName: string }> => {
    const token = getAuthToken();
    const formData = new FormData();
    formData.append('file', file);

    const response = await axios.post(`${API_URL}/upload`, formData, {
        headers: {
            'Content-Type': 'multipart/form-data',
            Authorization: `Bearer ${token}`
        }
    });
    return response.data;
};

const processFile = async (fileName: string, mapping: ColumnMapping): Promise<{ message: string }> => {
    const token = getAuthToken();
    const response = await axios.post(`${API_URL}/process`, { fileName, mapping }, {
        headers: {
            Authorization: `Bearer ${token}`
        }
    });
    return response.data;
};

const getHistory = async (): Promise<ImportJob[]> => {
    const token = getAuthToken();
    const response = await axios.get(`${API_URL}/history`, {
        headers: {
            Authorization: `Bearer ${token}`
        }
    });
    return response.data;
};

export const importService = {
    uploadFile,
    processFile,
    getHistory
};