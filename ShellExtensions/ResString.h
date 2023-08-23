#pragma once
#include <strsafe.h>
#include <Windows.h>

class CResStringW
{
    LPWSTR resource_str = nullptr;
public:
    LPWSTR String() {
        return this->resource_str;
    }

    CResStringW(HMODULE h_module, UINT res_id) {
        LPWSTR message = nullptr;
        int length = LoadStringW(h_module, res_id, (LPWSTR)&message, 0);
        if (0 == length) {
            this->resource_str = new WCHAR[1];
        }
        else
        {
            this->resource_str = new WCHAR[length + 1];
            if (FAILED(StringCchCopyNW(this->resource_str, length + 1, message, length))) {
                delete[] this->resource_str;
                this->resource_str = nullptr;
                this->resource_str = new WCHAR[1];
            }
        }
    }

    ~CResStringW() {
        delete[] this->resource_str;
    }
};