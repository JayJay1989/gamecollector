package com.gamecollector.app

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class BarcodeValueTest {
    @Test
    fun normalizesPrintedBarcodeSpacing() {
        assertEquals("887961751062", normalizeBarcode(" 8879 6175 1062 "))
    }

    @Test
    fun rejectsValuesOutsideSupportedLength() {
        assertNull(normalizeBarcode("1234567"))
        assertNull(normalizeBarcode("123456789012345"))
    }
}
