import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { platformService } from '../services/platformService';
import { PlatformRequest } from '../types/platform';

const RequestPlatformPage = () => {
    const [name, setName] = useState('');
    const [baseUrl, setBaseUrl] = useState('');
    const [commissionRate, setCommissionRate] = useState(0);
    const [error, setError] = useState<string | null>(null);
    const navigate = useNavigate();

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        try {
            const request: PlatformRequest = { name, baseUrl, commissionRate };
            await platformService.requestPlatform(request);
            navigate('/platforms');
        } catch (err) {
            setError('Failed to submit request');
        }
    };

    return (
        <div className="form-container">
            <h1>Request New Platform</h1>
            {error && <div>{error}</div>}
            <form onSubmit={handleSubmit}>
                <div className="input-group">
                    <label htmlFor="name">Name:</label>
                    <input id="name" type="text" value={name} onChange={e => setName(e.target.value)} required />
                </div>
                <div className="input-group">
                    <label htmlFor="baseUrl">Base URL:</label>
                    <input id="baseUrl" type="text" value={baseUrl} onChange={e => setBaseUrl(e.target.value)} />
                </div>
                <div className="input-group">
                    <label htmlFor="commissionRate">Commission Rate:</label>
                    <input id="commissionRate" type="number" value={commissionRate} onChange={e => setCommissionRate(parseFloat(e.target.value))} required inputMode="decimal" />
                </div>
                <button type="submit">Submit Request</button>
            </form>
        </div>
    );
};

export default RequestPlatformPage;