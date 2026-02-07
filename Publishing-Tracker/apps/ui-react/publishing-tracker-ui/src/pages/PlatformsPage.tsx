import { useEffect, useState, useRef } from 'react';
import { Link } from 'react-router-dom';
import { platformService } from '../services/platformService';
import { Platform } from '../types/platform';

const PlatformsPage = () => {
    const [platforms, setPlatforms] = useState<Platform[]>([]);
    const [searchTerm, setSearchTerm] = useState('');
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const debounceTimer = useRef<any>(null);

    const fetchPlatforms = async (search?: string) => {
        try {
            setLoading(true);
            const data = await platformService.getPlatforms(search);
            setPlatforms(data);
        } catch {
            setError('Failed to fetch integrated distribution channels.');
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchPlatforms();
    }, []);

    const handleSearchChange = (val: string) => {
        setSearchTerm(val);
        if (debounceTimer.current) clearTimeout(debounceTimer.current);
        debounceTimer.current = setTimeout(() => {
            fetchPlatforms(val);
        }, 500);
    };

    if (loading) return (
        <div style={pageStyles.centered}>
            <div className="shimmer" style={{ height: '100px', borderRadius: '16px', marginBottom: '2rem' }}></div>
            <div style={pageStyles.grid}>
                {[1, 2, 3, 4, 5, 6].map(i => <div key={i} className="shimmer" style={{ height: '240px', borderRadius: '16px' }}></div>)}
            </div>
        </div>
    );

    return (
        <div style={pageStyles.container}>
            <div style={pageStyles.header}>
                <div>
                    <h1>Sales Channels</h1>
                    <p style={pageStyles.subtitle}>Manage integrated publishing platforms and merchant identities.</p>
                </div>
                <Link to="/platforms/request" className="btn-primary animate-pulse">
                    <span style={{ fontSize: '1.2rem' }}>+</span> Request New Integration
                </Link>
            </div>

            <div className="card" style={pageStyles.searchCard}>
                <div style={pageStyles.searchWrapper}>
                    <span style={pageStyles.searchIcon}>🔍</span>
                    <input
                        type="text"
                        placeholder="Search marketplaces by name..."
                        value={searchTerm}
                        onChange={(e) => handleSearchChange(e.target.value)}
                        style={pageStyles.searchInput}
                    />
                </div>
            </div>

            {error && <div style={pageStyles.errorBanner}>{error}</div>}

            {platforms.length === 0 ? (
                <div style={pageStyles.emptyState}>
                    <div style={pageStyles.emptyIcon}>🌍</div>
                    <h3>Channel Not Found</h3>
                    <p>We don't currently have an integration for that marketplace.</p>
                    <Link to="/platforms/request" className="btn-primary" style={{ marginTop: '1.5rem' }}>
                        Request "{searchTerm}" Support
                    </Link>
                </div>
            ) : (
                <div style={pageStyles.grid}>
                    {platforms.map(platform => (
                        <div key={platform.id} className="card platform-card" style={pageStyles.platformCard}>
                            <div style={pageStyles.platformIcon}>
                                <span>{platform.name.charAt(0).toUpperCase()}</span>
                            </div>
                            <h3 style={pageStyles.platformName}>{platform.name}</h3>

                            <div style={pageStyles.infoBox}>
                                <div style={pageStyles.detailRow}>
                                    <span style={pageStyles.label}>Merchant Fee</span>
                                    <span style={pageStyles.rateBadge}>
                                        {(platform.commissionRate * 100).toFixed(0)}%
                                    </span>
                                </div>

                                <div style={pageStyles.detailRow}>
                                    <span style={pageStyles.label}>Network</span>
                                    <a href={platform.baseUrl} target="_blank" rel="noreferrer" style={pageStyles.link}>
                                        {platform.baseUrl ? platform.baseUrl.replace(/https?:\/\//, '').split('/')[0] : 'Standard'}
                                    </a>
                                </div>
                            </div>
                        </div>
                    ))}
                </div>
            )}

            <style>{`
                .platform-card {
                    transition: all 0.3s cubic-bezier(0.16, 1, 0.3, 1);
                    text-align: center;
                }
                .platform-card:hover {
                    transform: translateY(-8px);
                    border-color: var(--primary);
                    background: rgba(56, 189, 248, 0.05);
                    box-shadow: 0 20px 40px rgba(0,0,0,0.4);
                }
            `}</style>
        </div>
    );
};

const pageStyles = {
    container: { animation: 'fadeIn 0.5s ease-out' },
    header: { display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '3rem' },
    subtitle: { color: 'var(--text-muted)', fontSize: '1.1rem' },
    searchCard: { padding: '0.75rem', marginBottom: '2rem' },
    searchWrapper: { position: 'relative' as const, flex: 1, display: 'flex', alignItems: 'center' },
    searchIcon: { position: 'absolute' as const, left: '1.25rem', color: '#94a3b8', fontSize: '1.1rem' },
    searchInput: {
        width: '100%',
        padding: '0.85rem 1rem 0.85rem 3.25rem',
        background: 'transparent',
        border: 'none',
        fontSize: '1.1rem',
        color: '#fff'
    },
    grid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fill, minmax(300px, 1fr))',
        gap: '2rem',
    },
    platformCard: {
        display: 'flex',
        flexDirection: 'column' as const,
        alignItems: 'center',
        padding: '2.5rem',
        position: 'relative' as const,
        overflow: 'hidden'
    },
    platformIcon: {
        width: '72px',
        height: '72px',
        borderRadius: '20px',
        background: 'linear-gradient(135deg, var(--primary), var(--secondary))',
        color: 'white',
        fontSize: '2rem',
        fontWeight: '900',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        marginBottom: '1.5rem',
        boxShadow: '0 8px 16px rgba(0,0,0,0.2)'
    },
    platformName: { fontSize: '1.35rem', fontWeight: '800', marginBottom: '1.5rem', color: '#fff' },
    infoBox: { width: '100%', display: 'flex', flexDirection: 'column' as const, gap: '0.75rem' },
    detailRow: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        padding: '0.75rem 0',
        borderTop: '1px solid var(--border)'
    },
    label: { color: 'var(--text-muted)', fontSize: '0.75rem', fontWeight: '800', textTransform: 'uppercase' as const, letterSpacing: '0.05em' },
    rateBadge: {
        backgroundColor: 'rgba(16, 185, 129, 0.1)',
        color: 'var(--success)',
        padding: '4px 12px',
        borderRadius: '20px',
        fontWeight: '800',
        fontSize: '0.85rem'
    },
    link: {
        color: 'var(--primary)',
        textDecoration: 'none',
        fontSize: '0.85rem',
        fontWeight: '600',
        maxWidth: '160px',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap' as const
    },
    emptyState: { textAlign: 'center' as const, padding: '8rem 2rem' },
    emptyIcon: { fontSize: '4rem', marginBottom: '1.5rem' },
    errorBanner: { padding: '1.5rem', backgroundColor: 'rgba(239, 68, 68, 0.1)', color: 'var(--danger)', borderRadius: '16px', border: '1px solid var(--danger)', marginBottom: '2rem' },
    centered: { padding: '10rem 2rem' }
};

export default PlatformsPage;