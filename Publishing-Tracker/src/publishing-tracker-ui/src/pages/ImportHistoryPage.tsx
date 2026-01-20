import { useEffect, useState } from 'react';
import { importService } from '../services/importService';
import { ImportJob } from '../types/import';

const ImportHistoryPage = () => {
    const [history, setHistory] = useState<ImportJob[]>([]);
    const [loading, setLoading] = useState<boolean>(true);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        const fetchHistory = async () => {
            try {
                const historyData = await importService.getHistory();
                setHistory(historyData);
            } catch (err) {
                setError('Failed to fetch import history.');
            } finally {
                setLoading(false);
            }
        };
        fetchHistory();
    }, []);

    if (loading) return <div className="loading">Loading history...</div>;

    return (
        <div>
            <div style={pageStyles.header}>
                <h1>Import History</h1>
                <p style={{ color: 'var(--text-muted)' }}>View the status of your recent data synchronization tasks.</p>
            </div>

            {error && <div className="card" style={{ color: 'var(--danger)', marginBottom: '1rem' }}>{error}</div>}

            <div className="card" style={{ padding: 0, overflowX: 'auto' }}>
                <table style={pageStyles.table}>
                    <thead>
                        <tr style={pageStyles.tableHeader}>
                            <th style={pageStyles.th}>File Name</th>
                            <th style={pageStyles.th}>Status</th>
                            <th style={pageStyles.th}>Started At</th>
                            <th style={pageStyles.th}>Stats</th>
                        </tr>
                    </thead>
                    <tbody>
                        {history.length === 0 ? (
                            <tr>
                                <td colSpan={4} style={{ padding: '2rem', textAlign: 'center', color: 'var(--text-muted)' }}>
                                    No import history available.
                                </td>
                            </tr>
                        ) : (
                            history.map((job) => (
                                <tr key={job.id} style={pageStyles.row}>
                                    <td style={pageStyles.td}>
                                        <div style={{ fontWeight: '600' }}>{job.fileName}</div>
                                        <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>ID: {job.id}</div>
                                    </td>
                                    <td style={pageStyles.td}>
                                        <span style={{
                                            ...pageStyles.badge,
                                            ...getStatusStyle(job.status)
                                        }}>
                                            {job.status}
                                        </span>
                                    </td>
                                    <td style={pageStyles.td}>
                                        <div style={{ fontSize: '0.875rem' }}>{new Date(job.startedAt).toLocaleDateString()}</div>
                                        <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>
                                            {new Date(job.startedAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                                        </div>
                                    </td>
                                    <td style={pageStyles.td}>
                                        <div style={pageStyles.statsGrid}>
                                            <div title="Total Processed">📑 {job.recordsProcessed}</div>
                                            <div title="Successful" style={{ color: '#10b981' }}>✅ {job.recordsSuccessful}</div>
                                            <div title="Failed" style={{ color: 'var(--danger)' }}>❌ {job.recordsFailed}</div>
                                        </div>
                                    </td>
                                </tr>
                            ))
                        )}
                    </tbody>
                </table>
            </div>
        </div>
    );
};

// Helper to determine badge colors
const getStatusStyle = (status: string) => {
    switch (status.toUpperCase()) {
        case 'COMPLETED': return { backgroundColor: '#dcfce7', color: '#166534' };
        case 'FAILED': return { backgroundColor: '#fef2f2', color: '#991b1b' };
        case 'PROCESSING': return { backgroundColor: '#fef9c3', color: '#854d0e' };
        default: return { backgroundColor: '#f1f5f9', color: '#475569' };
    }
};

const pageStyles = {
    header: { marginBottom: '2rem' },
    table: { width: '100%', borderCollapse: 'collapse' as const, textAlign: 'left' as const },
    tableHeader: { backgroundColor: '#f8fafc', borderBottom: '1px solid var(--border)' },
    th: {
        padding: '1rem 1.5rem',
        fontSize: '0.75rem',
        textTransform: 'uppercase' as const,
        letterSpacing: '0.05em',
        color: 'var(--text-muted)',
        fontWeight: '700',
    },
    row: { borderBottom: '1px solid var(--border)', transition: 'background-color 0.2s' },
    td: { padding: '1rem 1.5rem', verticalAlign: 'middle' as const },
    badge: {
        padding: '4px 10px',
        borderRadius: '12px',
        fontSize: '0.75rem',
        fontWeight: '700',
        display: 'inline-block',
    },
    statsGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(3, 1fr)',
        gap: '0.5rem',
        fontSize: '0.85rem',
        fontWeight: '500',
    }
};

export default ImportHistoryPage;