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

    // Initial load for metadata
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

    // Fetch sales and summary on filter change
    useEffect(() => {
        const fetchData = async () => {
            setLoading(true);
            try {
                // Remove empty strings from filters before sending
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
        estimateSize: () => 50, // Slightly larger for better readability
    });

    if (loading) return <div className="loading">Loading ledger...</div>;
    if (error) return <div className="error-message">{error}</div>;

    return (
        <div>
            {/* Header with Action Button */}
            <div style={pageStyles.headerRow}>
                <h1>Sales Management</h1>
                <Link to="/sales/add" className="btn-primary">
                    + Add Manual Sale
                </Link>
            </div>

            {/* US-008: Sales Summary Ribbon */}
            <div style={pageStyles.summaryRibbon}>
                <div style={pageStyles.summaryCard}>
                    <label style={pageStyles.summaryLabel}>Total Revenue</label>
                    <div style={pageStyles.summaryValue}>${summary?.totalRevenue.toLocaleString() || '0.00'}</div>
                </div>
                <div style={pageStyles.summaryCard}>
                    <label style={pageStyles.summaryLabel}>Units Sold</label>
                    <div style={pageStyles.summaryValue}>{summary?.totalUnitsSold.toLocaleString() || '0'}</div>
                </div>
                <div style={pageStyles.summaryCard}>
                    <label style={pageStyles.summaryLabel}>Avg Royalty</label>
                    <div style={pageStyles.summaryValue}>${summary?.averageRoyalty.toFixed(2) || '0.00'}</div>
                </div>
            </div>

            {/* US-009: Filter Bar */}
            <div className="card" style={pageStyles.filterBar}>
                <div style={pageStyles.filterGroup}>
                    <label>Book</label>
                    <select name="bookId" value={filters.bookId || ''} onChange={handleFilterChange} style={pageStyles.filterSelect}>
                        <option value="">All Books</option>
                        {books.map((b: Book) => <option key={b.id} value={b.id}>{b.title}</option>)}
                    </select>
                </div>
                <div style={pageStyles.filterGroup}>
                    <label>Platform</label>
                    <select name="platformId" value={filters.platformId || ''} onChange={handleFilterChange} style={pageStyles.filterSelect}>
                        <option value="">All Platforms</option>
                        {platforms.map((p: Platform) => <option key={p.id} value={p.id}>{p.name}</option>)}
                    </select>
                </div>
                <div style={pageStyles.filterGroup}>
                    <label>From</label>
                    <input type="date" name="startDate" value={filters.startDate} onChange={handleFilterChange} style={pageStyles.filterInput} />
                </div>
                <div style={pageStyles.filterGroup}>
                    <label>To</label>
                    <input type="date" name="endDate" value={filters.endDate} onChange={handleFilterChange} style={pageStyles.filterInput} />
                </div>
            </div>

            {/* Main Data Container */}
            <div className="card" style={{ padding: 0, overflow: 'hidden' }}>
                {/* Table Header Labels */}
                <div style={pageStyles.tableHead}>
                    <div style={{ flex: 1 }}>Date</div>
                    <div style={{ flex: 2 }}>Book</div>
                    <div style={{ flex: 1.5 }}>Platform</div>
                    <div style={{ flex: 1 }}>Qty</div>
                    <div style={{ flex: 1 }}>Price</div>
                    <div style={{ flex: 1 }}>Royalty</div>
                    <div style={{ flex: 1, textAlign: 'right' }}>Revenue</div>
                </div>

                {/* Virtualized List Container */}
                <div
                    ref={parentRef}
                    style={{ height: '600px', overflow: 'auto', position: 'relative' }}
                >
                    <div style={{ height: `${rowVirtualizer.getTotalSize()}px`, width: '100%', position: 'relative' }}>
                        {rowVirtualizer.getVirtualItems().map(virtualItem => {
                            const sale = sales[virtualItem.index];
                            return (
                                <div
                                    key={virtualItem.key}
                                    style={{
                                        ...pageStyles.row,
                                        height: `${virtualItem.size}px`,
                                        transform: `translateY(${virtualItem.start}px)`
                                    }}
                                >
                                    <div style={{ flex: 1, color: 'var(--text-muted)', fontSize: '0.85rem' }}>
                                        {new Date(sale.saleDate).toLocaleDateString()}
                                    </div>
                                    <div style={{ flex: 2, fontWeight: '600', fontSize: '0.9rem' }}>
                                        {sale.bookTitle}
                                    </div>
                                    <div style={{ flex: 1.5 }}>
                                        <span style={pageStyles.platformBadge}>{sale.platformName}</span>
                                    </div>
                                    <div style={{ flex: 1, fontWeight: '500' }}>{sale.quantity}</div>
                                    <div style={{ flex: 1, fontSize: '0.9rem' }}>${sale.unitPrice.toFixed(2)}</div>
                                    <div style={{ flex: 1, fontSize: '0.9rem', color: '#059669' }}>${sale.royalty.toFixed(2)}</div>
                                    <div style={{ flex: 1, textAlign: 'right', fontWeight: '700', color: 'var(--primary)' }}>
                                        ${sale.revenue?.toFixed(2) || '0.00'}
                                    </div>
                                </div>
                            );
                        })}
                    </div>
                </div>
            </div>
        </div>
    );
};

const pageStyles = {
    headerRow: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        marginBottom: '2rem'
    },
    tableHead: {
        display: 'flex',
        padding: '1rem 1.5rem',
        backgroundColor: '#f1f5f9',
        borderBottom: '1px solid var(--border)',
        fontSize: '0.75rem',
        fontWeight: '700',
        textTransform: 'uppercase' as const,
        color: 'var(--text-muted)',
        letterSpacing: '0.05em'
    },
    row: {
        position: 'absolute' as const,
        left: 0,
        width: '100%',
        display: 'flex',
        alignItems: 'center',
        padding: '0 1.5rem',
        borderBottom: '1px solid var(--border)',
        boxSizing: 'border-box' as const,
    },
    platformBadge: {
        backgroundColor: '#eff6ff',
        color: '#1e40af',
        padding: '2px 8px',
        borderRadius: '4px',
        fontSize: '0.75rem',
        fontWeight: '500'
    },
    summaryRibbon: {
        display: 'grid',
        gridTemplateColumns: 'repeat(3, 1fr)',
        gap: '1.5rem',
        marginBottom: '2rem'
    },
    summaryCard: {
        backgroundColor: 'white',
        padding: '1.5rem',
        borderRadius: '12px',
        border: '1px solid var(--border)',
        boxShadow: '0 1px 3px rgba(0,0,0,0.05)'
    },
    summaryLabel: {
        display: 'block',
        fontSize: '0.75rem',
        color: 'var(--text-muted)',
        textTransform: 'uppercase' as const,
        marginBottom: '0.5rem',
        fontWeight: '600'
    },
    summaryValue: {
        fontSize: '1.75rem',
        fontWeight: '700',
        color: 'var(--text-main)'
    },
    filterBar: {
        display: 'flex',
        gap: '1.5rem',
        padding: '1.25rem 1.5rem',
        marginBottom: '1.5rem',
        flexWrap: 'wrap' as const,
        alignItems: 'flex-end'
    },
    filterGroup: {
        display: 'flex',
        flexDirection: 'column' as const,
        gap: '0.5rem',
        flex: 1,
        minWidth: '150px'
    },
    filterSelect: {
        padding: '0.6rem',
        borderRadius: '6px',
        border: '1px solid var(--border)',
        fontSize: '0.9rem'
    },
    filterInput: {
        padding: '0.55rem',
        borderRadius: '6px',
        border: '1px solid var(--border)',
        fontSize: '0.9rem'
    }
};

export default SalesPage;