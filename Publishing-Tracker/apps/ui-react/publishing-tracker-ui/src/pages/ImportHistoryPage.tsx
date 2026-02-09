import React, { useEffect, useState } from 'react';
import { importService } from '../services/importService';
import { ImportJob } from '../types/import';

const ImportHistoryPage = () => {
    const [history, setHistory] = useState<ImportJob[]>([]);
    const [loading, setLoading] = useState<boolean>(true);
    const [error, setError] = useState<string | null>(null);
    const [expandedJobId, setExpandedJobId] = useState<number | null>(null);

    useEffect(() => {
        const fetchHistory = async () => {
            try {
                const historyData = await importService.getHistory();
                setHistory(historyData);
            } catch {
                setError('Failed to fetch import history.');
            } finally {
                setLoading(false);
            }
        };
        fetchHistory();
    }, []);

    const toggleExpand = (id: number) => {
        setExpandedJobId(expandedJobId === id ? null : id);
    };

    if (loading) return <div style={pageStyles.centered}>Loading history ledger...</div>;

    return (
        <div style={pageStyles.container}>
            <div style={pageStyles.header}>
                <div>
                    <h1 style={pageStyles.title}>Import Logs</h1>
                    <p style={pageStyles.subtitle}>Audit trail of all automated data ingestion tasks.</p>
                </div>
            </div>

            {error && <div style={pageStyles.errorBanner}>{error}</div>}

            <div className="card" style={pageStyles.tableCard}>
                <table style={pageStyles.table}>
                    <thead>
                        <tr style={pageStyles.tableHeader}>
                            <th style={pageStyles.th}>Import Details</th>
                            <th style={pageStyles.th}>Status</th>
                            <th style={pageStyles.th}>Performance</th>
                            <th style={{ ...pageStyles.th, textAlign: 'right' }}>Actions</th>
                        </tr>
                    </thead>
                    <tbody>
                        {history.length === 0 ? (
                            <tr>
                                <td colSpan={4} style={pageStyles.emptyState}>
                                    No synchronization history found.
                                </td>
                            </tr>
                        ) : (
                            history.map((job) => {
                                const successRate = job.recordsProcessed > 0
                                    ? Math.round((job.recordsSuccessful / job.recordsProcessed) * 100)
                                    : 0;

                                return (
                                    <React.Fragment key={job.id}>
                                        <tr style={pageStyles.row}>
                                            <td style={pageStyles.td}>
                                                <div style={pageStyles.fileName}>{job.fileName}</div>
                                                <div style={pageStyles.subtext}>
                                                    {new Date(job.startedAt).toLocaleString([], { dateStyle: 'medium', timeStyle: 'short' })}
                                                </div>
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
                                                <div style={pageStyles.performanceWrapper}>
                                                    <div style={pageStyles.statsRow}>
                                                        <span style={{ color: '#10b981' }}>{job.recordsSuccessful} OK</span>
                                                        <span style={{ color: job.recordsFailed > 0 ? '#ef4444' : '#94a3b8' }}>
                                                            {job.recordsFailed} Fail
                                                        </span>
                                                    </div>
                                                    <div style={pageStyles.progressBarBase}>
                                                        <div style={{
                                                            ...pageStyles.progressBarFill,
                                                            width: `${successRate}%`,
                                                            backgroundColor: successRate === 100 ? '#10b981' : (successRate > 80 ? '#fbbf24' : '#ef4444')
                                                        }} />
                                                    </div>
                                                </div>
                                            </td>
                                            <td style={{ ...pageStyles.td, textAlign: 'right' }}>
                                                {(job.errorLog || job.recordsFailed > 0) && (
                                                    <button
                                                        onClick={() => toggleExpand(job.id)}
                                                        style={pageStyles.detailsBtn}
                                                    >
                                                        {expandedJobId === job.id ? 'Hide Logs' : 'View Logs'}
                                                    </button>
                                                )}
                                            </td>
                                        </tr>
                                        {expandedJobId === job.id && (
                                            <tr>
                                                <td colSpan={4} style={pageStyles.logCell}>
                                                    <div style={pageStyles.logContainer}>
                                                        <h4 style={pageStyles.logTitle}>Error Diagnostic Log</h4>
                                                        <pre style={pageStyles.logPre}>
                                                            {job.errorLog || 'No detailed error logs were generated for this task.'}
                                                        </pre>
                                                    </div>
                                                </td>
                                            </tr>
                                        )}
                                    </React.Fragment>
                                );
                            })
                        )}
                    </tbody>
                </table>
            </div>
        </div>
    );
};

// Helper for status badge styling
const getStatusStyle = (status: string) => {
    switch (status.toUpperCase()) {
        case 'COMPLETED': return { backgroundColor: '#dcfce7', color: '#166534', border: '1px solid #bbf7d0' };
        case 'FAILED': return { backgroundColor: '#fef2f2', color: '#991b1b', border: '1px solid #fecaca' };
        case 'PROCESSING': return { backgroundColor: '#fef9c3', color: '#854d0e', border: '1px solid #fef08a' };
        default: return { backgroundColor: '#f1f5f9', color: '#475569', border: '1px solid #e2e8f0' };
    }
};

const pageStyles = {
    container: { animation: 'fadeIn 0.4s ease' },
    header: { marginBottom: '2.5rem' },
    title: { fontSize: '1.875rem', fontWeight: '800', marginBottom: '0.25rem', color: '#1e293b' },
    subtitle: { color: 'var(--text-muted)' },
    tableCard: { padding: 0, overflow: 'hidden' },
    table: { width: '100%', borderCollapse: 'collapse' as const },
    tableHeader: { backgroundColor: '#f8fafc', borderBottom: '1px solid var(--border)' },
    th: {
        padding: '1rem 1.5rem',
        fontSize: '0.75rem',
        fontWeight: '700',
        color: '#64748b',
        textTransform: 'uppercase' as const,
        textAlign: 'left' as const,
        letterSpacing: '0.05em'
    },
    row: { borderBottom: '1px solid var(--border)', transition: 'background-color 0.15s' },
    td: { padding: '1.25rem 1.5rem', verticalAlign: 'top' as const },
    fileName: { fontWeight: '700', color: 'var(--text-main)', marginBottom: '0.25rem' },
    subtext: { fontSize: '0.8rem', color: 'var(--text-muted)' },
    badge: { padding: '4px 12px', borderRadius: '20px', fontSize: '0.7rem', fontWeight: '800', display: 'inline-block' },
    performanceWrapper: { width: '180px' },
    statsRow: { display: 'flex', justifyContent: 'space-between', fontSize: '0.75rem', fontWeight: '700', marginBottom: '6px' },
    progressBarBase: { height: '6px', backgroundColor: '#f1f5f9', borderRadius: '3px', overflow: 'hidden' },
    progressBarFill: { height: '100%', transition: 'width 0.5s ease-out' },
    detailsBtn: {
        backgroundColor: 'transparent',
        border: '1px solid var(--border)',
        padding: '6px 12px',
        borderRadius: '6px',
        fontSize: '0.8rem',
        fontWeight: '600',
        color: 'var(--text-muted)',
        cursor: 'pointer'
    },
    logCell: { backgroundColor: '#fdfdfd', padding: '0 1.5rem 1.5rem 1.5rem', borderBottom: '1px solid var(--border)' },
    logContainer: {
        backgroundColor: '#0f172a',
        padding: '1.5rem',
        borderRadius: '12px',
        marginTop: '0.5rem',
        boxShadow: 'inset 0 2px 4px rgba(0,0,0,0.2)'
    },
    logTitle: { color: '#94a3b8', fontSize: '0.8rem', textTransform: 'uppercase' as const, marginBottom: '1rem', letterSpacing: '0.1em' },
    logPre: { color: '#e2e8f0', fontSize: '0.85rem', margin: 0, whiteSpace: 'pre-wrap' as const, fontFamily: 'monaco, Consolas, "Lucida Console", monospace' },
    centered: { padding: '10rem', textAlign: 'center' as const, color: 'var(--text-muted)' },
    emptyState: { padding: '4rem', textAlign: 'center' as const, color: 'var(--text-muted)' },
    errorBanner: { padding: '1rem', backgroundColor: '#fef2f2', color: 'var(--danger)', borderRadius: '8px', marginBottom: '1rem', border: '1px solid #fee2e2' }
};

export default ImportHistoryPage;