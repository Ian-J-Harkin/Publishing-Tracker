import { useEffect, useState, useRef } from 'react';
import { Link } from 'react-router-dom';
import { saleService } from '../services/saleService';
import { Sale } from '../types/sale';
import { useVirtualizer } from '@tanstack/react-virtual';
import { theme } from '../styles/theme';

const SalesPage = () => {
    const [sales, setSales] = useState<Sale[]>([]);
    const [loading, setLoading] = useState<boolean>(true);
    const [error, setError] = useState<string | null>(null);
    const parentRef = useRef<HTMLDivElement>(null);

    useEffect(() => {
        const fetchSales = async () => {
            try {
                const salesData = await saleService.getSales();
                setSales(salesData);
            } catch (err) {
                setError('Failed to fetch sales transactions.');
            } finally {
                setLoading(false);
            }
        };
        fetchSales();
    }, []);

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
                <h1>Sales Transactions</h1>
                <Link to="/sales/add" className="btn-primary">
                    + Add Manual Sale
                </Link>
            </div>

            {/* Main Data Container */}
            <div className="card" style={{ padding: 0, overflow: 'hidden' }}>
                {/* Table Header Labels */}
                <div style={pageStyles.tableHead}>
                    <div style={{ flex: 1 }}>Date</div>
                    <div style={{ flex: 2 }}>Book Title</div>
                    <div style={{ flex: 1 }}>Platform</div>
                    <div style={{ flex: 1, textAlign: 'right' }}>Amount</div>
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
                                    <div style={{ flex: 1, color: 'var(--text-muted)', fontSize: '0.9rem' }}>
                                        {new Date(sale.saleDate).toLocaleDateString()}
                                    </div>
                                    <div style={{ flex: 2, fontWeight: '600' }}>
                                        {sale.bookTitle}
                                    </div>
                                    <div style={{ flex: 1 }}>
                                        <span style={pageStyles.platformBadge}>{sale.platformName}</span>
                                    </div>
                                    <div style={{ flex: 1, textAlign: 'right', fontWeight: '700', color: '#059669' }}>
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
        fontSize: '0.8rem',
        fontWeight: '500'
    }
};

export default SalesPage;