#!/usr/bin/env python3
"""
MODULE 48 — Global Auction House & Order-Book Matchmaker
Live bidding, escrow, price history, VIN tracking.
"""
import time
import uuid
from typing import Dict, List, Optional

class AuctionListing:
    def __init__(self, vin: str, seller: str, start_price: float, buyout: float, duration: float):
        self.id = str(uuid.uuid4())
        self.vin = vin
        self.seller = seller
        self.start_price = start_price
        self.buyout = buyout
        self.current_bid = start_price
        self.highest_bidder: Optional[str] = None
        self.ends_at = time.time() + duration
        self.bids: List[dict] = []
        self.sold = False

class AuctionHouse:
    def __init__(self):
        self.listings: Dict[str, AuctionListing] = {}
        self.escrow: Dict[str, float] = {}

    def create_listing(self, vin: str, seller: str, start: float, buyout: float, hours: float = 24) -> str:
        listing = AuctionListing(vin, seller, start, buyout, hours * 3600)
        self.listings[listing.id] = listing
        return listing.id

    def place_bid(self, listing_id: str, bidder: str, amount: float) -> bool:
        l = self.listings.get(listing_id)
        if not l or l.sold or time.time() > l.ends_at:
            return False
        if amount <= l.current_bid:
            return False
        l.current_bid = amount
        l.highest_bidder = bidder
        l.bids.append({"bidder": bidder, "amount": amount, "ts": time.time()})
        if amount >= l.buyout:
            self._finalize(l, bidder, amount)
        return True

    def _finalize(self, listing: AuctionListing, buyer: str, price: float):
        listing.sold = True
        self.escrow[listing.id] = price
        print(f"[Auction] {listing.vin} sold to {buyer} for {price}")

    def get_active(self) -> List[dict]:
        now = time.time()
        return [
            {"id": l.id, "vin": l.vin, "bid": l.current_bid, "buyout": l.buyout, "ends_in": max(0, l.ends_at - now)}
            for l in self.listings.values() if not l.sold and l.ends_at > now
        ]

if __name__ == "__main__":
    ah = AuctionHouse()
    lid = ah.create_listing("VIN123", "seller1", 5000, 15000, 1)
    ah.place_bid(lid, "buyer1", 6000)
    print(ah.get_active())
