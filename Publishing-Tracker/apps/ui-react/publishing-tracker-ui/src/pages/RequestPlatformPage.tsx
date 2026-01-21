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
            setError('Failed to submit request. Please check your network connection.');
        }
    };

    return (
        <div style={pageStyles.wrapper}>
            <div style={pageStyles.header}>
                <h1>Request New Platform</h1>
                <p style={{ color: 'var(--text-muted)' }}>
                    Don't see your marketplace? Request an integration and we'll review it.
                </p>
            </div>

            <div className="card" style={pageStyles.formCard}>
                {error && <div style={pageStyles.errorBanner}>{error}</div>}

                <form onSubmit={handleSubmit}>
                    <div className="form-group">
                        <label htmlFor="name">Platform Name</label>
                        <input 
                            id="name" 
                            type="text" 
                            value={name} 
                            onChange={e => setName(e.target.value)} 
                            placeholder="e.g. Google Play Books"
                            required 
                        />
                    </div>

                    <div className="form-group">
                        <label htmlFor="baseUrl">Platform Website (URL)</label>
                        <input 
                            id="baseUrl" 
                            type="url" 
                            value={baseUrl} 
                            onChange={e => setBaseUrl(e.target.value)} 
                            placeholder="https://example.com"
                        />
                    </div>

                    <div className="form-group">
                        <label htmlFor="commissionRate">Standard Commission Rate</label>
                        <input 
                            id="commissionRate" 
                            type="number" 
                            step="0.01"
                            value={commissionRate} 
                            onChange={e => setCommissionRate(parseFloat(e.target.value))} 
                            required 
                            inputMode="decimal" 
                        />
                        <small style={pageStyles.helperText}>
                            Enter as a decimal (e.g., 0.30 for 30%).
                        </small>
                    </div>

                    <div style={pageStyles.actions}>
                        <button type="button" onClick={() => navigate('/platforms')} style={pageStyles.cancelBtn}>
                            Cancel
                        </button>
                        <button type="submit" className="btn-primary">
                            Submit Request
                        </button>
                    </div>
                </form>
            </div>
        </div>
    );
};

const pageStyles = {
    wrapper: { maxWidth: '800px', margin: '0 auto' },
    header: { marginBottom: '2rem' },
    formCard: { padding: '2.5rem' },
    errorBanner: {
        backgroundColor: '#fef2f2',
        color: 'var(--danger)',
        padding: '1rem',
        borderRadius: '8px',
        marginBottom: '1.5rem',
        border: '1px solid #fee2e2',
        fontSize: '0.9rem',
    },
    helperText: {
        display: 'block',
        marginTop: '0.5rem',
        color: 'var(--text-muted)',
        fontSize: '0.8rem'
    },
    actions: {
        display: 'flex',
        justifyContent: 'flex-end',
        gap: '1rem',
        marginTop: '2rem',
        paddingTop: '1.5rem',
        borderTop: '1px solid var(--border)',
    },
    cancelBtn: {
        backgroundColor: 'transparent',
        border: 'none',
        color: 'var(--text-muted)',
        fontWeight: '600',
        cursor: 'pointer',
    }
};

export default RequestPlatformPage;