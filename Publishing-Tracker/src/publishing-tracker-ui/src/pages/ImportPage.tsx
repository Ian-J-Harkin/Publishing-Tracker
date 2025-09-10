import React, { useState } from 'react';
import { importService } from '../services/importService';
import { ColumnMapping } from '../types/import';

const ImportPage = () => {
    const [file, setFile] = useState<File | null>(null);
    const [mapping, setMapping] = useState<ColumnMapping>({
        bookTitle: '',
        platform: '',
        saleDate: '',
        quantity: '',
        unitPrice: '',
        royalty: ''
    });
    const [step, setStep] = useState<'upload' | 'mapping' | 'summary'>('upload');
    const [error, setError] = useState<string | null>(null);
    const [summary, setSummary] = useState<{ message: string } | null>(null);

    const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
        if (e.target.files) {
            setFile(e.target.files[0]);
        }
    };

    const handleUpload = async () => {
        if (file) {
            try {
                await importService.uploadFile(file);
                setStep('mapping');
            } catch (err) {
                setError('Failed to upload file.');
            }
        }
    };

    const handleMappingChange = (e: React.ChangeEvent<HTMLInputElement>) => {
        const { name, value } = e.target;
        setMapping(prevMapping => ({
            ...prevMapping,
            [name]: value
        }));
    };

    const handleProcess = async () => {
        if (file) {
            try {
                const result = await importService.processFile(file.name, mapping);
                setSummary(result);
                setStep('summary');
            } catch (err) {
                setError('Failed to process file.');
            }
        }
    };

    return (
        <div>
            <h1>Import Sales Data</h1>
            {error && <p style={{ color: 'red' }}>{error}</p>}

            {step === 'upload' && (
                <div>
                    <h2>Step 1: Upload CSV File</h2>
                    <input type="file" onChange={handleFileChange} accept=".csv,.txt" />
                    <button onClick={handleUpload} disabled={!file}>Upload</button>
                </div>
            )}

            {step === 'mapping' && (
                <div>
                    <h2>Step 2: Map Columns</h2>
                    {/* In a real application, you would display CSV headers and sample data here */}
                    <div>
                        <label>Book Title</label>
                        <input type="text" name="bookTitle" value={mapping.bookTitle} onChange={handleMappingChange} />
                    </div>
                    <div>
                        <label>Platform</label>
                        <input type="text" name="platform" value={mapping.platform} onChange={handleMappingChange} />
                    </div>
                    {/* Add other mapping fields here */}
                    <button onClick={handleProcess}>Process File</button>
                </div>
            )}

            {step === 'summary' && (
                <div>
                    <h2>Step 3: Import Summary</h2>
                    <p>{summary?.message}</p>
                </div>
            )}
        </div>
    );
};

export default ImportPage;