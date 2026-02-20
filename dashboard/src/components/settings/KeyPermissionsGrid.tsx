interface Permissions {
  canReadMarketData: boolean;
  canReadAccount: boolean;
  canTrade: boolean;
  canWithdraw: boolean;
}

interface Props {
  permissions: Permissions;
}

interface PermCell {
  label: string;
  key: keyof Permissions;
  description: string;
}

const CELLS: PermCell[] = [
  { label: 'Market Data', key: 'canReadMarketData', description: 'Read live prices and order book' },
  { label: 'Account Read', key: 'canReadAccount',   description: 'View balances and trade history' },
  { label: 'Trading',      key: 'canTrade',         description: 'Place and cancel orders' },
  { label: 'Withdrawal',   key: 'canWithdraw',      description: 'Withdraw funds from account' },
];

export function KeyPermissionsGrid({ permissions }: Props) {
  return (
    <div className="mt-3">
      <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-gray-400">
        Detected Permissions
      </p>
      <div className="grid grid-cols-2 gap-2">
        {CELLS.map((cell) => {
          const granted = permissions[cell.key];
          const isWithdraw = cell.key === 'canWithdraw';
          return (
            <div
              key={cell.key}
              className={`rounded-lg border p-3 ${
                granted
                  ? isWithdraw
                    ? 'border-red-500 bg-red-900/20'
                    : 'border-green-600 bg-green-900/20'
                  : 'border-gray-700 bg-gray-800/40'
              }`}
            >
              <div className="flex items-center gap-2">
                <span
                  className={`text-sm font-bold ${
                    granted ? (isWithdraw ? 'text-red-400' : 'text-green-400') : 'text-gray-600'
                  }`}
                >
                  {granted ? '\u2713' : '\u2715'}
                </span>
                <span
                  className={`text-sm font-medium ${
                    granted ? (isWithdraw ? 'text-red-300' : 'text-green-300') : 'text-gray-500'
                  }`}
                >
                  {cell.label}
                </span>
              </div>
              <p className="mt-1 text-xs text-gray-500">{cell.description}</p>
              {granted && isWithdraw && (
                <p className="mt-1 text-xs font-semibold text-red-400 uppercase tracking-wide">
                  Revoke Immediately
                </p>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
}
