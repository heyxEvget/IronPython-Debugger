class FibTest:
    def __init__(self, count):
        if count <= 0:
            raise ValueError("'count' must be larger than zero.")
        self.count = count

    def get_series(self, can_reverse=False):
        """
        Create a Fibonacci series, optionally reverse it, and
        return a result string.
        """
        series = self.fib(self.count)
        if can_reverse:
            series.reverse()
        return 'Fibonacci series up to ' + str(self.count) + \
               ': ' + str(series)

    @staticmethod
    def fib(n):
        """
        Generate a Fibonacci series up to n.
        """
        result = []
        a, b = 0, 1
        while b < n:
            result.append(b)    # add to the list
            a, b = b, a + b
        return result