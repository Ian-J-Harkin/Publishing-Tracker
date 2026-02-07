import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { platformService } from '../services/platformService';
import { Platform } from '../types/platform';

const PlatformsPage = () => {
    const [platforms, setPlatforms] = useState<Platform[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        const fetchPlatforms = async () => {
            try {
                const data = await platformService.getPlatforms();
                setPlatforms(data);
            } catch {
                setError('Failed to fetch platforms');
            } finally {
                setLoading(false);
            }
        };

        fetchPlatforms();
    }, []);

    if (loading) return <div className="loading">Loading platforms...</div>;

    return (
        <div>
            <div style={pageStyles.header}>
                <div>
                    <h1>Publishing Platforms</h1>
                    <p style={{ color: 'var(--text-muted)' }}>Manage the platforms where your books are currently listed.</p>
                </div>
                <Link to="/platforms/request" className="btn-primary">
                    + Request New Platform
                </Link>
            </div>

            {error && <div className="card" style={{ color: 'var(--danger)' }}>{error}</div>}

            <div style={pageStyles.grid}>
                {platforms.map(platform => (
                    <div key={platform.id} className="card" style={pageStyles.platformCard}>
                        <div style={pageStyles.platformIcon}>
                            {platform.name.charAt(0).toUpperCase()}
                        </div>
                        <h3 style={{ marginBottom: '0.5rem' }}>{platform.name}</h3>

                        <div style={pageStyles.detailRow}>
                            <span style={pageStyles.label}>Base URL:</span>
                            <a href={platform.baseUrl} target="_blank" rel="noreferrer" style={pageStyles.link}>
                                {platform.baseUrl.replace('https://', '')}
                            </a>
                        </div>

                        <div style={pageStyles.detailRow}>
                            <span style={pageStyles.label}>Commission:</span>
                            <span style={pageStyles.rateBadge}>
                                {(platform.commissionRate * 100).toFixed(0)}%
                            </span>
                        </div>
                    </div>
                ))}
            </div>
        </div>
    );
};

const pageStyles = {
    header: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        marginBottom: '2rem'
    },
    grid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))',
        gap: '1.5rem',
    },
    platformCard: {
        display: 'flex',
        flexDirection: 'column' as const,
        alignItems: 'center',
        textAlign: 'center' as const,
        padding: '2rem',
    },
    platformIcon: {
        width: '50px',
        height: '50px',
        borderRadius: '12px',
        backgroundColor: 'var(--primary)',
        color: 'white',
        fontSize: '1.5rem',
        fontWeight: '700',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        marginBottom: '1rem',
    },
    detailRow: {
        display: 'flex',
        justifyContent: 'space-between',
        width: '100%',
        marginTop: '0.75rem',
        fontSize: '0.9rem',
        borderTop: '1px solid var(--border)',
        paddingTop: '0.75rem'
    },
    label: {
        color: 'var(--text-muted)',
        fontWeight: '500'
    },
    link: {
        color: 'var(--primary)',
        textDecoration: 'none',
        maxWidth: '150px',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap' as const
    },
    rateBadge: {
        backgroundColor: '#f1f5f9',
        padding: '2px 8px',
        borderRadius: '6px',
        fontWeight: '600'
    }
};

export default PlatformsPage;