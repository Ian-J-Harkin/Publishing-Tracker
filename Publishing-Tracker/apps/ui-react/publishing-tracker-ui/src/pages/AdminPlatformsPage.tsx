import { useEffect, useState } from 'react';
import { platformService } from '../services/platformService';
import { Platform } from '../types/platform';

const AdminPlatformsPage = () => {
    const [platforms, setPlatforms] = useState<Platform[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    const fetchPlatforms = async () => {
        try {
            setLoading(true);
            const data = await platformService.getAdminPlatforms();
            setPlatforms(data);
        } catch {
            setError('Failed to fetch platforms.');
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchPlatforms();
    }, []);

    const handleApprove = async (id: number) => {
        try {
            await platformService.approvePlatform(id);
            // Refresh list to show it as active
            await fetchPlatforms();
        } catch {
            setError('Failed to approve platform.');
        }
    };

    if (loading) return <div style={pageStyles.centered}>Loading platforms...</div>;

    return (
        <div style={pageStyles.container}>
            <div style={pageStyles.header}>
                <div>
                    <h1 style={{ color: '#1e293b' }}>Manage Platforms</h1>
                    <p style={pageStyles.subtitle}>Approve pending integrations requested by users.</p>
                </div>
            </div>

            {error && <div style={pageStyles.errorBanner}>{error}</div>}

            <div className="card" style={{ padding: '0', overflow: 'hidden' }}>
                <table style={pageStyles.table}>
                    <thead>
                        <tr style={pageStyles.thRow}>
                            <th style={pageStyles.th}>ID</th>
                            <th style={pageStyles.th}>Name</th>
                            <th style={pageStyles.th}>Base URL</th>
                            <th style={pageStyles.th}>Commission</th>
                            <th style={pageStyles.th}>Status</th>
                            <th style={pageStyles.th}>Actions</th>
                        </tr>
                    </thead>
                    <tbody>
                        {platforms.length === 0 ? (
                            <tr>
                                <td colSpan={6} style={{ textAlign: 'center', padding: '2rem' }}>No platforms found.</td>
                            </tr>
                        ) : (
                            platforms.map(p => (
                                <tr key={p.id} style={pageStyles.tr}>
                                    <td style={pageStyles.td}>{p.id}</td>
                                    <td style={{ ...pageStyles.td, fontWeight: 'bold' }}>{p.name}</td>
                                    <td style={pageStyles.td}>{p.baseUrl || 'N/A'}</td>
                                    <td style={pageStyles.td}>{(p.commissionRate * 100).toFixed(1)}%</td>
                                    <td style={pageStyles.td}>
                                        <span style={p.isActive ? pageStyles.badgeActive : pageStyles.badgePending}>
                                            {p.isActive ? 'Active' : 'Pending Review'}
                                        </span>
                                    </td>
                                    <td style={pageStyles.td}>
                                        {!p.isActive && (
                                            <button 
                                                className="btn-primary" 
                                                style={{ padding: '0.4rem 0.8rem', fontSize: '0.85rem' }}
                                                onClick={() => handleApprove(p.id)}
                                            >
                                                Approve
                                            </button>
                                        )}
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

const pageStyles = {
    container: { animation: 'fadeIn 0.5s ease-out' },
    header: { display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '3rem' },
    subtitle: { color: 'var(--text-muted)', fontSize: '1.1rem' },
    errorBanner: { padding: '1.5rem', backgroundColor: 'rgba(239, 68, 68, 0.1)', color: 'var(--danger)', borderRadius: '16px', border: '1px solid var(--danger)', marginBottom: '2rem' },
    centered: { padding: '10rem 2rem', textAlign: 'center' as const, fontSize: '1.2rem', color: 'var(--text-muted)' },
    table: { width: '100%', borderCollapse: 'collapse' as const, textAlign: 'left' as const },
    thRow: { backgroundColor: 'rgba(0,0,0,0.02)' },
    th: { padding: '1rem 1.5rem', color: 'var(--text-muted)', fontSize: '0.85rem', textTransform: 'uppercase' as const, letterSpacing: '0.05em', borderBottom: '1px solid var(--border)' },
    tr: { borderBottom: '1px solid var(--border)', transition: 'background-color 0.2s' },
    td: { padding: '1rem 1.5rem', verticalAlign: 'middle' },
    badgeActive: { backgroundColor: 'rgba(16, 185, 129, 0.1)', color: 'var(--success)', padding: '4px 12px', borderRadius: '20px', fontWeight: '800', fontSize: '0.75rem', textTransform: 'uppercase' as const },
    badgePending: { backgroundColor: 'rgba(245, 158, 11, 0.1)', color: '#d97706', padding: '4px 12px', borderRadius: '20px', fontWeight: '800', fontSize: '0.75rem', textTransform: 'uppercase' as const },
};

export default AdminPlatformsPage;
