import { useEffect, useState, useRef } from 'react';
import { Link } from 'react-router-dom';
import { bookService } from '../services/bookService';
import { Book } from '../types/book';
import { useVirtualizer } from '@tanstack/react-virtual';

const BookListPage = () => {
    const [books, setBooks] = useState<Book[]>([]);
    const [loading, setLoading] = useState<boolean>(true);
    const [error, setError] = useState<string | null>(null);

    // Moved hooks to the top of the component to ensure they are called on every render,
    // fixing a violation of the Rules of Hooks.
    const parentRef = useRef<HTMLDivElement>(null);

    const rowVirtualizer = useVirtualizer({
        count: books.length,
        getScrollElement: () => parentRef.current,
        estimateSize: () => 35,
    });

    useEffect(() => {
        const fetchBooks = async () => {
            try {
                const booksData = await bookService.getBooks();
                setBooks(booksData);
            } catch (err) {
                setError('Failed to fetch books.');
            } finally {
                setLoading(false);
            }
        };

        fetchBooks();
    }, []);

    const handleDelete = async (id: number) => {
        if (window.confirm('Are you sure you want to delete this book?')) {
            try {
                await bookService.deleteBook(id);
                setBooks(books.filter(book => book.id !== id));
            } catch (err) {
                setError('Failed to delete book.');
            }
        }
    };

    if (loading) {
        return <div>Loading...</div>;
    }

    if (error) {
        return <div>{error}</div>;
    }

    return (
        <div>
            <h1>My Books</h1>
            <Link to="/books/add">Add New Book</Link>
            <div ref={parentRef} style={{ height: '400px', overflow: 'auto' }}>
                <div style={{ height: `${rowVirtualizer.getTotalSize()}px`, width: '100%', position: 'relative' }}>
                    {rowVirtualizer.getVirtualItems().map(virtualItem => (
                        <div key={virtualItem.key} style={{ position: 'absolute', top: 0, left: 0, width: '100%', height: `${virtualItem.size}px`, transform: `translateY(${virtualItem.start}px)` }}>
                            <Link to={`/books/edit/${books[virtualItem.index].id}`}>{books[virtualItem.index].title}</Link> by {books[virtualItem.index].author}
                            <button onClick={() => handleDelete(books[virtualItem.index].id)}>Delete</button>
                        </div>
                    ))}
                </div>
            </div>
        </div>
    );
};

export default BookListPage;