﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Moises.Toolkit.Collections
{
    public class IncrementalLoadingCollection<TSource, IType> : ObservableRangeCollection<IType>, IIncrementalLoadingCollection where TSource : IIncrementalSource<IType>
    {
        public event EventHandler StartLoading;

        public event EventHandler EndLoading;

        public event EventHandler<Exception> Error;

        public event EventHandler LoadingCompleted;

        protected TSource Source { get; }

        protected int ItemsPerPage { get; }

        protected int CurrentPageIndex { get; set; }

        private bool _isLoading;
        private bool _hasMoreItems;
        private CancellationToken _cancellationToken;
        private bool _refreshOnLoad;

        public bool IsLoading
        {
            get
            {
                return _isLoading;
            }

            private set
            {
                if (value != _isLoading)
                {
                    _isLoading = value;
                    OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsLoading)));

                    if (_isLoading)
                    {
                        StartLoading?.Invoke(this, new EventArgs());
                    }
                    else
                    {
                        EndLoading?.Invoke(this, new EventArgs());
                    }
                }
            }
        }

        public IEnumerable<IType> CollectionElements { get; set; }

        public Object[] Args { get; set; }

        public bool HasMoreItems
        {
            get
            {
                if (_cancellationToken.IsCancellationRequested)
                {
                    return false;
                }

                return _hasMoreItems;
            }

            private set
            {
                if (value != _hasMoreItems)
                {
                    _hasMoreItems = value;
                    OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasMoreItems)));
                }
            }
        }


        public IncrementalLoadingCollection(int itemsPerPage = 50)
            : this(Activator.CreateInstance<TSource>(), itemsPerPage)
        {
        }
        public IncrementalLoadingCollection(TSource source, int itemsPerPage = 50)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            Source = source;

            ItemsPerPage = itemsPerPage;
            _hasMoreItems = true;
        }

        private void OnLoadingCompleted()
        {
            this.LoadingCompleted?.Invoke(this, new EventArgs());
        }

        public async Task<uint> LoadMoreItemsAsync(long count = 1)
        {
            return await LoadMoreItemsAsync(count, new CancellationToken(false));
        }

        public async Task RefreshAsync()
        {
            if (IsLoading)
            {
                _refreshOnLoad = true;
            }
            else
            {
                Clear();
                CurrentPageIndex = 0;
                HasMoreItems = true;
                await LoadMoreItemsAsync(1);
            }
        }

        protected virtual async Task<IEnumerable<IType>> LoadDataAsync(CancellationToken cancellationToken)
        {
            var result = await Source.GetPagedItemsAsync(CurrentPageIndex++, ItemsPerPage, cancellationToken, Args);
            return result;
        }

        private async Task<uint> LoadMoreItemsAsync(long count, CancellationToken cancellationToken)
        {
            uint resultCount = 0;
            _cancellationToken = cancellationToken;

            try
            {
                if (!_cancellationToken.IsCancellationRequested)
                {
                    IEnumerable<IType> data = null;
                    try
                    {
                        IsLoading = true;
                        data = (await LoadDataAsync(_cancellationToken));
                    }
                    catch (OperationCanceledException)
                    {
                        // The operation has been canceled using the Cancellation Token.
                    }
                    catch (Exception ex)
                    {
                        Error?.Invoke(this, ex);
                    }

                    if (data != null && data.Any() && !_cancellationToken.IsCancellationRequested)
                    {
                        resultCount = (uint)data.Count();
                        this.AddRange(data);
                        /*foreach (var item in data)
                        {
                            this.Add(item);
                        }*/
                    }
                    else
                    {
                        HasMoreItems = false;
                    }
                }
            }
            finally
            {
                IsLoading = false;

                if (_refreshOnLoad)
                {
                    _refreshOnLoad = false;
                    await RefreshAsync();
                }
                OnLoadingCompleted();
            }

            return resultCount;
        }

    }
}