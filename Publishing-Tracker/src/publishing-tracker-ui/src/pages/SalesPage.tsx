import { useEffect, useState, useRef } from 'react';
import { Link } from 'react-router-dom';
import { saleService } from '../services/saleService';
import { Sale } from '../types/sale';
import { useVirtualizer } from '@tanstack/react-virtual';

const SalesPage = () => {
    const [sales, setSales] = useState<Sale[]>([]);
    const [loading, setLoading] = useState<boolean>(true);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        const fetchSales = async () => {
            try {
                const salesData = await saleService.getSales();
                setSales(salesData);
            } catch (err) {
                setError('Failed to fetch sales.');
            } finally {
                setLoading(false);
            }
        };

        fetchSales();
    }, []);

    if (loading) {
        return <div>Loading...</div>;
    }

    if (error) {
        return <div>{error}</div>;
    }

    const parentRef = useRef<HTMLDivElement>(null);

    const rowVirtualizer = useVirtualizer({
        count: sales.length,
        getScrollElement: () => parentRef.current,
        estimateSize: () => 35,
    });

    return (
        <div>
            <h1>Sales Data</h1>
            <Link to="/sales/add">Add Manual Sale</Link>
            <div ref={parentRef} style={{ height: '400px', overflow: 'auto' }}>
                <div style={{ height: `${rowVirtualizer.getTotalSize()}px`, width: '100%', position: 'relative' }}>
                    {rowVirtualizer.getVirtualItems().map(virtualItem => (
                        <div key={virtualItem.key} style={{ position: 'absolute', top: 0, left: 0, width: '100%', height: `${virtualItem.size}px`, transform: `translateY(${virtualItem.start}px)` }}>
                            {new Date(sales[virtualItem.index].saleDate).toLocaleDateString()} - {sales[virtualItem.index].bookTitle} - {sales[virtualItem.index].platformName}
                        </div>
                    ))}
                </div>
            </div>
        </div>
    );
};

export default SalesPage;