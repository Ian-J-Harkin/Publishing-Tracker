import React from 'react';
import { useNavigate } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { platformService } from '../services/platformService';

const requestSchema = z.object({
    name: z.string().min(2, 'Platform identity must be at least 2 characters'),
    baseUrl: z.string().url('A valid distribution URL is required').or(z.literal('')),
    commissionRate: z.number().min(0).max(1, 'Rate must be between 0.00 and 1.00 (e.g. 0.30 for 30%)'),
});

type RequestFormData = z.infer<typeof requestSchema>;

const RequestPlatformPage = () => {
    const navigate = useNavigate();
    const {
        register,
        handleSubmit,
        formState: { errors, isSubmitting }
    } = useForm<RequestFormData>({
        resolver: zodResolver(requestSchema),
        defaultValues: {
            name: '',
            baseUrl: '',
            commissionRate: 0.30
        }
    });

    const [submissionStatus, setSubmissionStatus] = React.useState<'idle' | 'success' | 'error'>('idle');

    const onSubmit = async (data: RequestFormData) => {
        try {
            setSubmissionStatus('idle');
            await platformService.requestPlatform(data);
            setSubmissionStatus('success');
            setTimeout(() => navigate('/platforms'), 2000);
        } catch {
            setSubmissionStatus('error');
        }
    };

    if (submissionStatus === 'success') {
        return (
            <div style={pageStyles.centered}>
                <div style={pageStyles.successContainer}>
                    <div style={pageStyles.successIcon}>✓</div>
                    <h2>Request Transmitted</h2>
                    <p style={{ color: 'var(--text-muted)' }}>
                        Our data synchronization team has received your integration request.
                        Redirecting to the distribution index...
                    </p>
                </div>
            </div>
        );
    }

    return (
        <div style={pageStyles.wrapper}>
            <div style={pageStyles.header}>
                <h1>Request Integration</h1>
                <p style={{ color: 'var(--text-muted)', fontSize: '1.1rem' }}>
                    Expand your reach. Request new marketplace support for our analytics engine.
                </p>
            </div>

            <div className="card" style={pageStyles.formCard}>
                {submissionStatus === 'error' && (
                    <div style={pageStyles.errorBanner}>
                        <strong>Network Failure:</strong> Unable to secure the request at this time.
                    </div>
                )}

                <form onSubmit={handleSubmit(onSubmit)}>
                    <div className="form-group">
                        <label>Platform Identity</label>
                        <input
                            {...register('name')}
                            placeholder="e.g. Google Play Books, Kobo Writing Life"
                            style={errors.name ? { borderColor: 'var(--danger)' } : {}}
                        />
                        {errors.name && <small style={pageStyles.errorText}>{errors.name.message}</small>}
                    </div>

                    <div className="form-group">
                        <label>Base Distribution URL</label>
                        <input
                            {...register('baseUrl')}
                            placeholder="https://publish.example.com"
                            style={errors.baseUrl ? { borderColor: 'var(--danger)' } : {}}
                        />
                        {errors.baseUrl && <small style={pageStyles.errorText}>{errors.baseUrl.message}</small>}
                    </div>

                    <div className="form-group">
                        <label>Average Commission Rate (0.00 - 1.00)</label>
                        <input
                            type="number"
                            step="0.01"
                            {...register('commissionRate', { valueAsNumber: true })}
                            style={errors.commissionRate ? { borderColor: 'var(--danger)' } : {}}
                        />
                        <small style={pageStyles.helperText}>
                            Input the decimal merchant fee (e.g., 0.30 represents a 30% flat fee).
                        </small>
                        {errors.commissionRate && <small style={pageStyles.errorText}>{errors.commissionRate.message}</small>}
                    </div>

                    <div style={pageStyles.infoBox}>
                        <div style={{ fontSize: '1.5rem' }}>ℹ️</div>
                        <div style={{ fontSize: '0.85rem', color: 'var(--text-muted)' }}>
                            Requests are reviewed for API compatibility and data structure alignment.
                            Approved platforms will appear for all users.
                        </div>
                    </div>

                    <div style={pageStyles.actions}>
                        <button type="button" onClick={() => navigate('/platforms')} style={pageStyles.cancelBtn}>
                            Discard Request
                        </button>
                        <button type="submit" className="btn-primary" disabled={isSubmitting}>
                            {isSubmitting ? 'Transmitting...' : 'Commit Request'}
                        </button>
                    </div>
                </form>
            </div>
        </div>
    );
};

const pageStyles = {
    wrapper: { maxWidth: '750px', margin: '0 auto', animation: 'fadeIn 0.5s ease' },
    header: { marginBottom: '3rem' },
    formCard: { padding: '3rem', border: '1px solid rgba(255,255,255,0.05)' },
    infoBox: {
        padding: '1.25rem',
        backgroundColor: 'rgba(56, 189, 248, 0.05)',
        borderRadius: '12px',
        border: '1px solid rgba(56, 189, 248, 0.1)',
        display: 'flex',
        gap: '1rem',
        alignItems: 'center',
        marginTop: '2rem'
    },
    errorBanner: {
        backgroundColor: 'rgba(239, 68, 68, 0.1)',
        color: 'var(--danger)',
        padding: '1rem',
        borderRadius: '12px',
        marginBottom: '2rem',
        border: '1px solid var(--danger)',
        fontSize: '0.95rem',
        fontWeight: '600'
    },
    errorText: { color: 'var(--danger)', fontSize: '0.8rem', fontWeight: '600', marginTop: '4px' },
    helperText: { color: 'var(--text-muted)', fontSize: '0.75rem', marginTop: '4px', fontWeight: '500' },
    actions: {
        display: 'flex',
        justifyContent: 'flex-end',
        gap: '2rem',
        marginTop: '3rem',
        paddingTop: '2rem',
        borderTop: '1px solid var(--border)',
        alignItems: 'center'
    },
    cancelBtn: {
        backgroundColor: 'transparent',
        border: 'none',
        color: 'var(--text-muted)',
        fontWeight: '700',
        cursor: 'pointer',
        fontSize: '0.95rem',
        textTransform: 'uppercase' as const,
        letterSpacing: '0.1em'
    },
    centered: { padding: '15rem 2rem', textAlign: 'center' as const },
    successContainer: { animation: 'fadeIn 0.5s ease' },
    successIcon: {
        width: '64px',
        height: '64px',
        borderRadius: '20px',
        backgroundColor: 'var(--success)',
        color: '#fff',
        fontSize: '2rem',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        margin: '0 auto 1.5rem',
        boxShadow: '0 0 20px rgba(16, 185, 129, 0.4)'
    }
};

export default RequestPlatformPage;