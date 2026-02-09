import { useEffect, useState, useRef } from 'react';
import { Link } from 'react-router-dom';
import { saleService } from '../services/saleService';
import { bookService } from '../services/bookService';
import { platformService } from '../services/platformService';
import { Sale, SalesSummary, SalesFilter } from '../types/sale';
import { Book } from '../types/book';
import { Platform } from '../types/platform';
import { useVirtualizer } from '@tanstack/react-virtual';

const SalesPage = () => {
    const [sales, setSales] = useState<Sale[]>([]);
    const [summary, setSummary] = useState<SalesSummary | null>(null);
    const [books, setBooks] = useState<Book[]>([]);
    const [platforms, setPlatforms] = useState<Platform[]>([]);
    const [filters, setFilters] = useState<SalesFilter>({
        bookId: undefined,
        platformId: undefined,
        startDate: '',
        endDate: ''
    });
    const [loading, setLoading] = useState<boolean>(true);
    const [error, setError] = useState<string | null>(null);
    const parentRef = useRef<HTMLDivElement>(null);

    useEffect(() => {
        const loadMetadata = async () => {
            try {
                const [availableBooks, availablePlatforms] = await Promise.all([
                    bookService.getBooks(),
                    platformService.getPlatforms()
                ]);
                setBooks(availableBooks);
                setPlatforms(availablePlatforms);
            } catch (err) {
                console.error("Failed to load metadata", err);
            }
        };
        loadMetadata();
    }, []);

    useEffect(() => {
        const fetchData = async () => {
            setLoading(true);
            try {
                const cleanFilters = Object.fromEntries(
                    Object.entries(filters).filter(([_, v]) => v !== '' && v !== undefined)
                );

                const [salesData, summaryData] = await Promise.all([
                    saleService.getSales(cleanFilters),
                    saleService.getSummary(cleanFilters)
                ]);
                setSales(salesData);
                setSummary(summaryData);
                setError(null);
            } catch {
                setError('Failed to fetch sales statistics.');
            } finally {
                setLoading(false);
            }
        };
        fetchData();
    }, [filters]);

    const handleFilterChange = (e: React.ChangeEvent<HTMLSelectElement | HTMLInputElement>) => {
        const { name, value } = e.target;
        setFilters((prev: SalesFilter) => ({
            ...prev,
            [name]: value === "" ? undefined : (name.endsWith('Id') ? parseInt(value) : value)
        }));
    };

    const rowVirtualizer = useVirtualizer({
        count: sales.length,
        getScrollElement: () => parentRef.current,
        estimateSize: () => 64,
    });

    if (loading && sales.length === 0) return (
        <div style={pageStyles.centered}>
            <div className="shimmer" style={{ height: '100px', borderRadius: '16px', marginBottom: '2rem' }}></div>
            <div className="shimmer" style={{ height: '600px', borderRadius: '16px' }}></div>
        </div>
    );

    return (
        <div style={pageStyles.container}>
            <div style={pageStyles.headerRow}>
                <div>
                    <h1 style={{ marginBottom: '0.25rem', color: '#1e293b' }}>Sales Ledger</h1>
                    <p style={{ color: 'var(--text-muted)' }}>Manage your verified royalty streams and transactions.</p>
                </div>
                <Link to="/sales/add" className="btn-primary animate-pulse">
                    <span style={{ fontSize: '1.2rem' }}>+</span> Add Manual Entry
                </Link>
            </div>

            <div style={pageStyles.summaryRibbon}>
                <div className="card metric-card" style={pageStyles.summaryCard}>
                    <label style={pageStyles.summaryLabel}>Total Revenue</label>
                    <div style={pageStyles.summaryValue}>${summary?.totalRevenue.toLocaleString(undefined, { minimumFractionDigits: 2 }) || '0.00'}</div>
                </div>
                <div className="card metric-card" style={pageStyles.summaryCard}>
                    <label style={pageStyles.summaryLabel}>Global Unit Sales</label>
                    <div style={{ ...pageStyles.summaryValue, color: 'var(--secondary)' }}>{summary?.totalUnitsSold.toLocaleString() || '0'}</div>
                </div>
                <div className="card metric-card" style={pageStyles.summaryCard}>
                    <label style={pageStyles.summaryLabel}>Avg Royalty/Unit</label>
                    <div style={{ ...pageStyles.summaryValue, color: 'var(--accent)' }}>${summary?.averageRoyalty.toFixed(2) || '0.00'}</div>
                </div>
            </div>

            <div className="card" style={pageStyles.filterBar}>
                <div style={pageStyles.filterGroup}>
                    <label>Filter by Publication</label>
                    <select name="bookId" value={filters.bookId || ''} onChange={handleFilterChange}>
                        <option value="">All Works</option>
                        {books.map((b: Book) => <option key={b.id} value={b.id}>{b.title}</option>)}
                    </select>
                </div>
                <div style={pageStyles.filterGroup}>
                    <label>Sales Channel</label>
                    <select name="platformId" value={filters.platformId || ''} onChange={handleFilterChange}>
                        <option value="">All Platforms</option>
                        {platforms.map((p: Platform) => <option key={p.id} value={p.id}>{p.name}</option>)}
                    </select>
                </div>
                <div style={pageStyles.filterGroup}>
                    <label>Start Date</label>
                    <input type="date" name="startDate" value={filters.startDate} onChange={handleFilterChange} />
                </div>
                <div style={pageStyles.filterGroup}>
                    <label>End Date</label>
                    <input type="date" name="endDate" value={filters.endDate} onChange={handleFilterChange} />
                </div>
            </div>

            <div className="card" style={{ padding: 0, overflow: 'hidden' }}>
                <div style={pageStyles.tableHead}>
                    <div style={{ flex: 1 }}>Date</div>
                    <div style={{ flex: 2 }}>Title</div>
                    <div style={{ flex: 1.5 }}>Channel</div>
                    <div style={{ flex: 0.8 }}>Qty</div>
                    <div style={{ flex: 1 }}>Royalty</div>
                    <div style={{ flex: 1, textAlign: 'right' }}>Revenue</div>
                </div>

                <div ref={parentRef} style={pageStyles.scrollContainer}>
                    {sales.length === 0 && !loading ? (
                        <div style={{ padding: '5rem', textAlign: 'center', color: 'var(--text-muted)' }}>
                            <h3>No matching sales found</h3>
                            <p>Try adjusting your search parameters.</p>
                        </div>
                    ) : (
                        <div style={{ height: `${rowVirtualizer.getTotalSize()}px`, width: '100%', position: 'relative' }}>
                            {rowVirtualizer.getVirtualItems().map(virtualItem => {
                                const sale = sales[virtualItem.index];
                                if (!sale) return null;
                                return (
                                    <div
                                        key={virtualItem.key}
                                        className="row-hover"
                                        style={{
                                            ...pageStyles.row,
                                            height: `${virtualItem.size}px`,
                                            transform: `translateY(${virtualItem.start}px)`
                                        }}
                                    >
                                        <div style={{ flex: 1, color: 'var(--text-muted)', fontSize: '0.8rem', fontWeight: 'bold' }}>
                                            {new Date(sale.saleDate).toLocaleDateString([], { month: 'short', day: 'numeric', year: 'numeric' })}
                                        </div>
                                        <div style={{ flex: 2, fontWeight: '700', fontSize: '1rem', color: '#1e293b' }}>
                                            {sale.bookTitle}
                                        </div>
                                        <div style={{ flex: 1.5 }}>
                                            <span style={pageStyles.platformBadge}>{sale.platformName}</span>
                                        </div>
                                        <div style={{ flex: 0.8, fontWeight: '800', color: 'var(--secondary)' }}>{sale.quantity}</div>
                                        <div style={{ flex: 1, fontSize: '0.9rem', color: 'var(--success)', fontWeight: '600' }}>
                                            ${sale.royalty.toFixed(2)}
                                        </div>
                                        <div style={{ flex: 1, textAlign: 'right', fontWeight: '900', color: 'var(--primary)', fontSize: '1.1rem' }}>
                                            ${sale.revenue?.toFixed(2) || '0.00'}
                                        </div>
                                    </div>
                                );
                            })}
                        </div>
                    )}
                </div>
            </div>

            <style>{`
                .row-hover {
                    transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1);
                    cursor: default;
                }
                .row-hover:hover {
                    background: rgba(255, 255, 255, 0.05) !important;
                    transform: scale(1.002) translateY(0) !important;
                    z-index: 10;
                    box-shadow: 0 4px 20px rgba(0,0,0,0.2);
                }
            `}</style>
        </div>
    );
};

const pageStyles = {
    container: { animation: 'fadeIn 0.5s ease-out' },
    headerRow: { display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '3rem' },
    summaryRibbon: { display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: '1.5rem', marginBottom: '2.5rem' },
    summaryCard: { padding: '2rem', display: 'flex', flexDirection: 'column' as const, gap: '0.5rem' },
    summaryLabel: { fontSize: '0.75rem', color: 'var(--text-muted)', fontWeight: '800', textTransform: 'uppercase' as const, letterSpacing: '0.1em' },
    summaryValue: { fontSize: '2.25rem', fontWeight: '900', color: '#1e293b', letterSpacing: '-0.02em' },
    filterBar: { display: 'flex', gap: '2rem', padding: '2rem', marginBottom: '2rem', flexWrap: 'wrap' as const, alignItems: 'center' },
    filterGroup: { display: 'flex', flexDirection: 'column' as const, gap: '0.65rem', flex: 1, minWidth: '200px' },
    tableHead: {
        display: 'flex',
        padding: '1.25rem 2rem',
        background: 'rgba(255,255,255,0.02)',
        borderBottom: '1px solid var(--border)',
        fontSize: '0.7rem',
        fontWeight: '800',
        textTransform: 'uppercase' as const,
        color: '#64748b',
        letterSpacing: '0.1em'
    },
    scrollContainer: { height: '600px', overflow: 'auto', position: 'relative' as const },
    row: {
        position: 'absolute' as const,
        left: 0,
        width: '100%',
        display: 'flex',
        alignItems: 'center',
        padding: '0 2rem',
        borderBottom: '1px solid var(--border)',
        boxSizing: 'border-box' as const,
    },
    platformBadge: {
        backgroundColor: 'rgba(56, 189, 248, 0.1)',
        color: 'var(--primary)',
        padding: '6px 14px',
        borderRadius: '20px',
        fontSize: '0.7rem',
        fontWeight: '800',
        letterSpacing: '0.05em',
        border: '1px solid rgba(56, 189, 248, 0.2)'
    },
    centered: { padding: '5rem', textAlign: 'center' as const }
};

export default SalesPage;