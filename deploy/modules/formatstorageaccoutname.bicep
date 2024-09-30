// format a container name
// Azure storage account names are 3-24 characters long; lower-case letters, numbers, no hyphens, no underscores or special character; must start and end with a letter or number
@description('Format an event name following the Azure Eventhub naming conventions')
param storageAccountName string
var lowercasedName = toLower(storageAccountName)
var replaceUndercores = replace(lowercasedName, '_', '')
var replaceDashes2 = replace(replaceUndercores, '-', '')
var removeInvalidChars1 = replace(
  replace(
    replace(
      replace(
        replace(
          replace(
            replace(
              replace(
                replace(
                  replace(
                    replaceDashes2,
                    '!', ''
                  ),
                  '@', ''
                ),
                '#', ''
              ),
              '$', ''
            ),
            '%', ''
          ),
          '^', ''
        ),
        '&', ''
      ),
      '*', ''
    ),
    '(', ''
  ),
  ')', ''
)

var removeInvalidChars2 = replace(
  replace(
    replace(
      replace(
        replace(
          replace(
            replace(
              replace(
                replace(
                  replace(
                    removeInvalidChars1,
                    '+', ''
                  ),
                  '=', ''
                ),
                '/', ''
              ),
              '\\', ''
            ),
            ',', ''
          ),
          '.', ''
        ),
        '"', ''
      ),
      '\'', ''
    ),
    '[', ''
  ),
  ']', ''
)

var removeInvalidChars3 = replace(
  replace(
    replace(
      replace(
        replace(
          replace(
            replace(
              removeInvalidChars2,
              '{', ''
            ),
            '}', ''
          ),
          '|', ''
        ),
        '`', ''
      ),
      '?', ''
    ),
    '>', ''
  ),
  '<', ''
)

var trimStart = startsWith(removeInvalidChars2, '-') ? substring(removeInvalidChars2, 1, length(removeInvalidChars2) - 1) : removeInvalidChars2
var trimEnd = endsWith(trimStart, '-') ? substring(removeInvalidChars3, length(trimStart) - 1) : trimStart
var minSizeName = length(trimEnd) < 4 ? '${trimEnd}-stgacct' : trimEnd

output formattedStorageAccountName string = take(minSizeName, 23)
